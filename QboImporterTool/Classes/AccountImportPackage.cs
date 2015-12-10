using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using DLI.Core.Common.Enums.QuickBooksOnline;
using DLI.Core.Common.Requests.QuickBooksOnline;
using DLI.Core.Common.Responses.QuickBooksOnline;
using Excel.Log.Logger;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using QboImporterTool.Mapper;
using RestSharp;

namespace QboImporterTool.Classes
{
    internal class AccountImportPackage : BaseImportPackage<SaveQuickBooksOnlineAccountRequest, QuickBooksOnlineAccountResponse>
    {
        internal new AccountMapper Mapper; 
        private decimal _totalTimeTaken;
        private int _maxSubLevel = 1;

        private readonly List<List<QbOnlineBatchItemRequest>> _requestsDividedBySublevel = new List<List<QbOnlineBatchItemRequest>>();

        public AccountImportPackage(string fileName)
        {
            FilePath = fileName;
            RawDataSet = Utils.GetRowsFromExcelFile(fileName);
            PayLoad = new List<QbOnlineBatchItemRequest>();
            TypeOfImport = typeof(SaveQuickBooksOnlineAccountRequest);
            ItemChoiceType = ItemChoiceType6.Account;
            ImportType = ImportableTypes.Account;

            var allAccountNames = new List<string>();
            for (var x = 0; x < RawDataSet.Count; x++)
            {
                allAccountNames.Add(RawDataSet[x]["Account"].ToString());
            }
            Mapper = new AccountMapper(allAccountNames);
        }

        public override void ExtractRequestsFromRows()
        {
            _maxSubLevel = 0;
            _requestsDividedBySublevel.Clear();
            
            //Mapper.NamesUsed.Clear();

            for (var x = 0; x < RawDataSet.Count; x++)
            {
                var row = RawDataSet[x];
                var requestEntity = GetBaseRequestFromRow(row, Mapper);

                PayLoad.Add(new QbOnlineBatchItemRequest()
                {
                    Entity = requestEntity,
                    BatchId = x.ToString(),
                    OperationType = (OperationEnum)(int)requestEntity.RequestType,
                    ItemElementName = ItemChoiceType
                });
            }

            _maxSubLevel = PayLoad.Select(x => ((SaveQuickBooksOnlineAccountRequest) x.Entity).SubLevel).Max();

            //After this for loop the requestsDividedBySubLevel will have a list as an element for each sublevel... so requestsDividedBySubLevel(0) would be all parent accounts,
            //requestsDividedBySubLevel(maxSubLevel) is the furthest down child account.
            for (var subLevel = 1; subLevel <= _maxSubLevel; subLevel++)
            {
                _requestsDividedBySublevel.Add(PayLoad.Where(x => ((SaveQuickBooksOnlineAccountRequest)x.Entity).SubLevel == subLevel).ToList());
            }
        }

        protected override SaveQuickBooksOnlineAccountRequest GetBaseRequestFromRow(DataRow row, IRowToRequestMapper<SaveQuickBooksOnlineAccountRequest, QuickBooksOnlineAccountResponse> mapper)
        {
            var existing = mapper.GetExistingFromRow(row);
            var newRequest = new SaveQuickBooksOnlineAccountRequest()
            {
                EntityTypeName = ImportType.ToString(),
                RequestType = existing != null ? RequestType.Update : RequestType.Create,
                SyncToken = existing != null ? existing.SyncToken : null,
                Id = existing != null ? existing.EntityId : null
            };
            newRequest = mapper.AddMappingsToBaseRequest(row, newRequest);
            return newRequest;
        }

        public override void SendPayloadToQbo()
        {
            for (var i = 0; i < _requestsDividedBySublevel.Count; i++)
            {
                var requestsToSend = _requestsDividedBySublevel[i];
                requestsToSend = DoAccountImportLogic(requestsToSend);
                foreach (var req in
                    from req in requestsToSend
                    let convertedReq = (SaveQuickBooksOnlineAccountRequest) req.Entity
                    where !convertedReq.IsLastAccountInHierarchy
                    select req)
                {
                    ((SaveQuickBooksOnlineAccountRequest) req.Entity).OpeningBalance = 0;
                    ((SaveQuickBooksOnlineAccountRequest) req.Entity).OpeningBalanceDate = DateTime.Now;
                    ((SaveQuickBooksOnlineAccountRequest) req.Entity).UseOpeningBalance = true;
                }
                DoSingleSend(requestsToSend,(i+1));
            }

            Console.WriteLine();
            Console.WriteLine(@"Import operation(s) completed for {0}s...", ImportType.ToString());
            Console.WriteLine(@"Total time taken: {0} seconds...", TimeTaken / 1000);
            Console.WriteLine(@"Press ENTER to continue.");
            Console.ReadLine();
        }

        private static List<QbOnlineBatchItemRequest> DoAccountImportLogic(IEnumerable<QbOnlineBatchItemRequest> requestsToSend)
        {
            Program.CurrentAccounts = Utils.GetAllEntities<QuickBooksOnlineAccountResponse>("Account");
            
            var qbOnlineBatchItemRequests = requestsToSend as QbOnlineBatchItemRequest[] ?? requestsToSend.ToArray();
            
            foreach (var subAccountRequest in qbOnlineBatchItemRequests)
            {
                var batchRequestEntity = (SaveQuickBooksOnlineAccountRequest) subAccountRequest.Entity;

                if (batchRequestEntity.AccountType == AccountType.AccountsPayable || batchRequestEntity.AccountType == AccountType.AccountsReceivable)
                {
                    batchRequestEntity.OpeningBalance = 0;
                    batchRequestEntity.OpeningBalanceDate = null;
                }

                AccountSubType subType;
                if (Enum.TryParse(batchRequestEntity.Name.Replace(" ", "").Trim(), out subType))
                {
                    batchRequestEntity.AccountDetail = subType;
                }

                subAccountRequest.Entity = batchRequestEntity;

                if (Program.CurrentAccounts.All(x => x.Name != batchRequestEntity.ParentAccountName)) continue;

                batchRequestEntity.ParentAccountId = Program.CurrentAccounts.Find(x => x.Name == batchRequestEntity.ParentAccountName).EntityId;
                batchRequestEntity.IsSubAccount = true;
            }
            
            return qbOnlineBatchItemRequests.ToList();
        }

        private void DoSingleSend(List<QbOnlineBatchItemRequest> listOfRequestsToSend, int subLevel)
        {
            Console.WriteLine(@"Sending Batch Request for Accounts at sublevel {0}...", subLevel);
            var restClient = new RestClient(Program.QboIntegrationDomain) { Timeout = 300000 };
            var request = new RestRequest("api/QuickBooks/Batch/DoBatch", Method.POST);
            request.AddJsonBody(listOfRequestsToSend);
            request.AddHeader("authToken", "db55d34e-eacd-42cb-b1ba-48f724b35103");
            Console.WriteLine(@"Sent...");
            Console.Write(@"Awaiting Response...");
            restClient.ExecuteAsync(request, OnResponseReceived);
            WaitingForResponse = true;
            _totalTimeTaken += TimeTaken;
            TimeTaken = 0;

            while (WaitingForResponse)
            {
                System.Threading.Thread.Sleep(250);
                TimeTaken += 250;
                Console.Write(@".");
            }
            Console.WriteLine();
            Console.WriteLine("Accounts at sublevel {0} completed. Press ENTER to continue.", subLevel);
            Console.ReadLine();
        }

        public override void OnResponseReceived(IRestResponse response)
        {
            if (response.ResponseStatus != ResponseStatus.Completed)
            {
                Console.WriteLine();
                Console.WriteLine(@"Import operation not completed... response from QBO returned with status: " +
                                  response.ResponseStatus.ToString());
            }

            //var serializer = new JsonDeserializer();
            var data = JsonConvert.DeserializeObject<List<QbOnlineBatchItemResponse>>(response.Content);

            var warningBatchResponses = new List<QbOnlineBatchItemResponse>();
            var errorBatchResponses = new List<QbOnlineBatchItemResponse>();
            foreach (var batchItem in data.ToArray())
            {
                var error = ((JObject)batchItem.Entity).ToObject<QbFaultResponse>();
                if (error.Errors != null)
                    errorBatchResponses.Add(batchItem);
                if (batchItem.Warnings != null && batchItem.Warnings.Length > 0)
                    warningBatchResponses.Add(batchItem);
            }

            if (warningBatchResponses.Any())
            {
                Console.WriteLine();
                Console.WriteLine("Warnings in some of the requests returned:");
                foreach (var warningResponse in warningBatchResponses)
                {
                    Console.WriteLine("BatchId {0}:", warningResponse.BatchId);
                    foreach (var warning in warningResponse.Warnings)
                    {
                        Console.WriteLine("Code: {0}", warning.Code);
                        Console.WriteLine("Detail: {0}", warning.Detail);
                        Console.WriteLine("Element: {0}", warning.Element);
                        Console.WriteLine("Message: {0}", warning.Message);
                    }
                    Console.WriteLine();
                    Console.WriteLine();
                }
            }

            if (errorBatchResponses.Any())
            {
                Console.WriteLine();
                Console.WriteLine("Errors in some of the requests returned:");
                foreach (var errorBatchResponse in errorBatchResponses)
                {
                    var faultResponse = ((JObject)errorBatchResponse.Entity).ToObject<QbFaultResponse>();

                    if (faultResponse == null) continue;

                    Console.WriteLine("BatchId {0}:", errorBatchResponse.BatchId);
                    foreach (var error in faultResponse.Errors)
                    {
                        Console.WriteLine("Code: {0}", error.Code);
                        Console.WriteLine("Detail: {0}", error.Detail);
                        Console.WriteLine("Element: {0}", error.Element);
                        Console.WriteLine("Message: {0}", error.Message);
                        Console.WriteLine();
                        Console.WriteLine();
                    }
                }
            }
            WaitingForResponse = false;
        }
    }

    internal class AccountMapper : IRowToRequestMapper<SaveQuickBooksOnlineAccountRequest, QuickBooksOnlineAccountResponse>
    {
        internal List<string> NamesUsed = new List<string>();
        private readonly List<string> _allAccountNames;
        public AccountMapper(List<string> allAccountNames)
        {
            _allAccountNames = allAccountNames;
        }
        public SaveQuickBooksOnlineAccountRequest AddMappingsToBaseRequest(DataRow row, SaveQuickBooksOnlineAccountRequest accountRequest )
        {
            var accountNameString = row["Account"].ToString();
            var accountInfo = GetAccountInfo(accountNameString);

            accountRequest.Active = true;
            accountRequest.FullyQualifiedName = accountNameString;
            accountRequest.UseOpeningBalance = true; //This won't apply to accounts rreceivable or accounts payable, it switched to false later in the Sending of the batches for those accounts
            accountRequest.Name = accountInfo.AccountName;
            accountRequest.IsSubAccount = accountInfo.IsSubAccount;
            accountRequest.ParentAccountName = accountInfo.IsSubAccount ? accountInfo.ParentAccountName : "";
            accountRequest.OpeningBalanceDate = DateTime.Now;
            accountRequest.OpeningBalance = row["Balance Total"] is DBNull ? 0 : Convert.ToDecimal(row["Balance Total"]);
            accountRequest.Description = row["Description"].ToString();
            accountRequest.AccountType = (AccountType) Enum.Parse(typeof (AccountType), row["Type"].ToString().Replace(" ", ""));
            accountRequest.SubLevel = accountNameString.Count(x => x == ':') + 1;
            accountRequest.IsLastAccountInHierarchy = accountInfo.IsLastInAccountHierarchy; //We will only be importing balances for the last accounts in sub account hierarchy. which
                                                                                            //will automatically update the account balance for their parent accounts.

            NamesUsed.Add(accountInfo.AccountName);

            return accountRequest;
        }

        public QuickBooksOnlineAccountResponse GetExistingFromRow(DataRow row)
        {
            var accountNameString = row["Account"].ToString();
            var accountInfo = GetAccountInfo(accountNameString);
            return Program.CurrentAccounts.Find(x => x.Name == accountInfo.AccountName);
        }

        private AccountInfo GetAccountInfo(string accountNameString)
        {
            var accountInfo=  new AccountInfo()
            {
                IsSubAccount = accountNameString.Contains(":"),
                AccountName = "",
                ParentAccountName = "",
                IsLastInAccountHierarchy = false
            };

            if (accountInfo.IsSubAccount)
            {
                var indexOfLastColon = accountNameString.LastIndexOf(":", StringComparison.Ordinal);
                var indexOfNextToLastColon = Utils.GetPrevColonFromIndexInString(indexOfLastColon, accountNameString);
                accountInfo.ParentAccountName = indexOfNextToLastColon == -1 ? accountNameString.Substring(0, indexOfLastColon).Trim() :
                                                                   accountNameString.Substring(indexOfNextToLastColon + 1, (indexOfLastColon - (indexOfNextToLastColon + 1))).Trim();
                accountInfo.AccountName = accountNameString.Substring(indexOfLastColon + 1).Trim();
            }
            else
            {
                accountInfo.AccountName = accountNameString;
            }

            accountInfo.IsLastInAccountHierarchy = _allAccountNames.Any(x => x.EndsWith(":" + accountInfo.AccountName));

            var suffix = 2;

            while (NamesUsed.Any(x => x == accountInfo.AccountName))
            {
                accountInfo.AccountName += " " + suffix;
                suffix++;
            }
            return accountInfo;
        }

        struct AccountInfo
        {
            public bool IsLastInAccountHierarchy;
            public bool IsSubAccount;
            public string AccountName;
            public string ParentAccountName;

            public AccountInfo(bool isLastInAccountHierarchy) : this()
            {
                this.IsLastInAccountHierarchy = isLastInAccountHierarchy;
            }
        }
    }
}
