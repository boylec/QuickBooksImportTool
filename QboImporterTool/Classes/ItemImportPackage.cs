using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DLI.Core.Common.Enums.QuickBooksOnline;
using DLI.Core.Common.Requests.QuickBooksOnline;
using DLI.Core.Common.Responses.QuickBooksOnline;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using QboImporterTool.Classes.Bases;
using QboImporterTool.Mapper;
using RestSharp;

namespace QboImporterTool.Classes
{
    internal class ItemImportPackage :
        BaseImportPackage<SaveQuickBooksOnlineItemRequest, QuickBooksOnlineItemResponse>
    {
        internal new ItemMapper Mapper; 
        private int _maxSubLevel = 1;
        private decimal _totalTimeTaken;

        private readonly List<List<QbOnlineBatchItemRequest>> _requestsDividedBySublevel = new List<List<QbOnlineBatchItemRequest>>();

        public ItemImportPackage(string fileName)
        {
            Logger.Instance.Log("Began import package for Item");
            FilePath = fileName;
            RawDataSet = Utils.GetRowsFromExcelFile(fileName);
            PayLoad = new List<QbOnlineBatchItemRequest>();
            TypeOfImport = typeof(SaveQuickBooksOnlineItemRequest);
            ItemChoiceType = ItemChoiceType6.Item;
            ImportType = ImportableTypes.Item;
            Mapper = new ItemMapper();
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

            _maxSubLevel = PayLoad.Select(x => ((SaveQuickBooksOnlineItemRequest)x.Entity).SubLevel).Max();

            //After this for loop the requestsDividedBySubLevel will have a list as an element for each sublevel... so requestsDividedBySubLevel(0) would be all parent accounts,
            //requestsDividedBySubLevel(maxSubLevel) is the furthest down child account.
            for (var subLevel = 1; subLevel <= _maxSubLevel; subLevel++)
            {
                _requestsDividedBySublevel.Add(PayLoad.Where(x => ((SaveQuickBooksOnlineItemRequest)x.Entity).SubLevel == subLevel).ToList());
            }
        }

        protected override SaveQuickBooksOnlineItemRequest GetBaseRequestFromRow(DataRow row, IRowToRequestMapper<SaveQuickBooksOnlineItemRequest, QuickBooksOnlineItemResponse> mapper)
        {
            var existing = mapper.GetExistingFromRow(row);
            var newRequest = new SaveQuickBooksOnlineItemRequest()
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
                requestsToSend = DoItemImportLogic(requestsToSend);
                DoSingleSend(requestsToSend, (i + 1));
            }

            Console.WriteLine();
            Console.WriteLine(@"Import operation(s) completed for {0}s...", ImportType.ToString());
            Console.WriteLine(@"Total time taken: {0} seconds...", TimeTaken / 1000);
            Console.WriteLine(@"Press ENTER to continue.");
            Console.ReadLine();
        }

        private static List<QbOnlineBatchItemRequest> DoItemImportLogic(IEnumerable<QbOnlineBatchItemRequest> requestsToSend)
        {
            Program.CurrentItems = Utils.GetAllEntities<QuickBooksOnlineItemResponse>("item");

            var qbOnlineBatchItemRequests = requestsToSend as QbOnlineBatchItemRequest[] ?? requestsToSend.ToArray();

            foreach (var batchRequestEntity in qbOnlineBatchItemRequests
                    .Select(subitemRequest => (SaveQuickBooksOnlineItemRequest)subitemRequest.Entity)
                    .Where(batchRequestEntity => Program.CurrentItems
                                                .Any(x => x.Name == batchRequestEntity.ParentItemName)))
            {
                batchRequestEntity.ParentItemId = Program.CurrentItems.Find(x => x.Name == batchRequestEntity.ParentItemName).EntityId;
                batchRequestEntity.IsSubItem = true;
            }

            return qbOnlineBatchItemRequests.ToList();
        }

        private void DoSingleSend(List<QbOnlineBatchItemRequest> listOfRequestsToSend, int subLevel)
        {
            Console.WriteLine(@"Sending Batch Request for Items at sublevel {0}...", subLevel);
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
            Console.WriteLine("Items at sublevel {0} completed. Press ENTER to continue.", subLevel);
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
    

    internal class ItemMapper : IRowToRequestMapper<SaveQuickBooksOnlineItemRequest, QuickBooksOnlineItemResponse>
    {
        internal List<string> NamesUsed = new List<string>();

        public SaveQuickBooksOnlineItemRequest AddMappingsToBaseRequest(DataRow row,
            SaveQuickBooksOnlineItemRequest request)
        {
            var itemNameString = row["Item"].ToString();
            var itemInfo = GetItemInfo(itemNameString);
            var preferredVendorName = row["Preferred Vendor"].ToString();
            var itemPrice = row["Price"] is DBNull ? 0 : Convert.ToDecimal(row["Price"].ToString());
            request.Description = row["Description"].ToString();
            //var isDiscountItem = (request.Description!= null && request.Description.StartsWith("Discount", StringComparison.CurrentCultureIgnoreCase));
            request.ItemCategoryType = ItemCategoryType.Service;
            request.ItemType = ItemType.Service;
            request.Name = itemInfo.ItemName;
            request.IsSubItem = itemInfo.IsSubItem;
            if (request.IsSubItem)
            {
                request.ParentItemName = itemInfo.ParentItemName;
            }
            request.Price = itemPrice;
            request.IncomeAccountRefId = //isDiscountItem
                //? Program.CurrentAccounts.Find(x => x.FullyQualifiedName == "Discounts").EntityId :
                Program.CurrentAccounts.Find(x => x.Name == row["Account"].ToString()).EntityId;
            request.UseIncomeAccount = true;
            request.ExpenseAccountRefId = !(row["COGS Account"] is DBNull)
                ? Program.CurrentAccounts.Find(x => x.Name == row["COGS Account"].ToString()).EntityId
                : null;
            request.UseExpenseAccount = request.ExpenseAccountRefId != null;
            request.SubLevel = itemNameString.Count(x => x == ':') + 1;
            if(!string.IsNullOrEmpty(preferredVendorName))
                request.PreferredVendorId = Program.CurrentVendors.Find(x => x.DisplayName == preferredVendorName).EntityId;
            return request;
        }

        public QuickBooksOnlineItemResponse GetExistingFromRow(DataRow row)
        {
            var itemNameString = row["Item"].ToString();
            var itemInfo = GetItemInfo(itemNameString);
            return Program.CurrentItems.Find(x => x.Name == itemInfo.ItemName);

        }

        private ItemInfo GetItemInfo(string itemNameString)
        {

            var itemInfo = new ItemInfo()
            {
                IsSubItem = itemNameString.Contains(":"),
                ItemName = "",
                ParentItemName = ""
            };

            if (itemInfo.IsSubItem)
            {
                var indexOfLastColon = itemNameString.LastIndexOf(":", StringComparison.Ordinal);
                var indexOfNextToLastColon = Utils.GetPrevColonFromIndexInString(indexOfLastColon, itemNameString);
                itemInfo.ParentItemName = indexOfNextToLastColon == -1 ? itemNameString.Substring(0, indexOfLastColon).Trim() :
                                                                   itemNameString.Substring(indexOfNextToLastColon + 1, (indexOfLastColon - (indexOfNextToLastColon + 1))).Trim();
                itemInfo.ItemName = itemNameString.Substring(indexOfLastColon + 1).Trim();
            }
            else
            {
                itemInfo.ItemName = itemNameString;
            }

            var suffix = 2;

            while (NamesUsed.Any(x => x == itemInfo.ItemName))
            {
                itemInfo.ItemName += " " + suffix;
                suffix++;
            }
            return itemInfo;
        }

        struct ItemInfo
        {
            public bool IsSubItem;
            public string ItemName;
            public string ParentItemName;
        }
    }
}
