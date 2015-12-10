using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using DLI.Core.Common.DTOs.QuickBooksOnline;
using DLI.Core.Common.Enums.QuickBooksOnline;
using DLI.Core.Common.Requests.QuickBooksOnline;
using DLI.Core.Common.Responses.QuickBooksOnline;
using QboImporterTool.Mapper;
using RestSharp;

namespace QboImporterTool.Classes.Bases
{
    internal class MultiFileImportPackage<TRequestType, TResponseType>:
        BaseImportPackage<TRequestType, TResponseType>
        where TRequestType : QbOnlineRequest, new()
        where TResponseType : QbOnlineResponse, new()
    {
        protected DataRowCollection DetailDataSet { get; set; }
        //protected List<InvoiceImportPackage> InvoiceImports { get; set; }
        protected new IParentChildMapAdder<TRequestType, TResponseType> Mapper;
        public MultiFileImportPackage(string parentFile, string detailFile, IParentChildMapAdder<TRequestType, TResponseType> mapAdder, ImportableTypes importType)
        {
            FilePath = parentFile;
            Mapper = mapAdder;
            PayLoad = new List<QbOnlineBatchItemRequest>();
            TypeOfImport = typeof(TRequestType);
            ItemChoiceType = (ItemChoiceType6)Enum.Parse(typeof(ItemChoiceType6), importType.ToString());
            ImportType = importType;
            RawDataSet = Utils.GetRowsFromExcelFile(parentFile);
            DetailDataSet = Utils.GetRowsFromExcelFile(detailFile);
        }

        public override void ExtractRequestsFromRows()
        {
            var totalInvoices = RawDataSet.Count;
            Console.WriteLine("Extracting requests from " + RawDataSet.Count + " rows for {0} entities.", ImportType.ToString());
            Console.WriteLine("With a total " + DetailDataSet.Count + " detail items to package.");
            for (var x = 0; x < RawDataSet.Count; x++)
            {
                Console.Write("\r" + ImportType + ": " + (x+1) + "/ " + totalInvoices);
                
                var row = RawDataSet[x];
                if (!Mapper.IsParentRowValid(row)) continue;
                
                var parentItem = new MultiFileImportDataSet {ParentRow = row};

                parentItem.DetailRows = Mapper.GetDetailRows(row, DetailDataSet, parentItem);

                var requestEntity = GetBaseRequestFromRow(parentItem, Mapper);

                PayLoad.Add(new QbOnlineBatchItemRequest()
                {
                    Entity = requestEntity,
                    BatchId = x.ToString(),
                    OperationType = (OperationEnum) (int) requestEntity.RequestType,
                    ItemElementName = ItemChoiceType
                });
            }
            Console.WriteLine();
            Console.WriteLine("Complete! Processing entities sent to QBO sometimes takes awhile...");
        }

        public override void SendPayloadToQbo()
        {
            var batchPortions = new List<List<QbOnlineBatchItemRequest>>();
            for (var x = 0; x < PayLoad.Count; x++)
            {
                var portion = new List<QbOnlineBatchItemRequest>();
                for (var y = 0; y < PayLoad.Count/80; y++)
                {
                    if (x <= PayLoad.Count - 1)
                    {
                        portion.Add(PayLoad[x]);
                        x++;
                    }
                    else
                        break;
                }
                batchPortions.Add(portion);
            }

            TimeTaken = 0;
            var portionsCompleted = 0;
            decimal estimatedSecondsRemaining = 600;
            Console.WriteLine(@"Sending Batch Request with all packaged " + ItemChoiceType.ToString() + " save requests to QBO...");
            var timeOfLastPortion = 0M;
            foreach (var portion in batchPortions)
            {
                var restClient = new RestClient(Program.QboIntegrationDomain) { Timeout = 300000000 };
                var request = new RestRequest("api/QuickBooks/Batch/DoBatch", Method.POST);
                request.AddJsonBody(portion);
                request.AddHeader("authToken", "db55d34e-eacd-42cb-b1ba-48f724b35103");
                //Console.WriteLine(@"Sent...");
                //Console.Write(@"Awaiting Response...");
                restClient.ExecuteAsync(request, OnResponseReceived);
                WaitingForResponse = true;
                while (WaitingForResponse)
                {
                    System.Threading.Thread.Sleep(250);
                    TimeTaken += 250;
                    estimatedSecondsRemaining -= .25M;
                    timeOfLastPortion += 250;
                    var percentComplete = Convert.ToDecimal((decimal)portionsCompleted/batchPortions.Count) * 100M;
                    var minutesRemaining = Math.Floor(estimatedSecondsRemaining/60);
                    var secondsRemaining = estimatedSecondsRemaining%60;
                    Console.Write("\rPercentage Complete: {0}%, Estimated time remaining: {1} minutes, {2} seconds", percentComplete.ToString("0.00"), minutesRemaining.ToString("0"), secondsRemaining.ToString("0"));
                }
                portionsCompleted++;
                estimatedSecondsRemaining = (timeOfLastPortion)*(batchPortions.Count - portionsCompleted)/1000M;
                timeOfLastPortion = 0;
            }
            Console.Write("\rPercentage Complete: 100%");
            
            Console.WriteLine();
            Console.WriteLine(@"Import operation(s) completed for {0}s...", ImportType.ToString());
            Console.WriteLine(@"Total time taken: {0} seconds...", TimeTaken / 1000);
            Console.WriteLine(@"Press ENTER to continue.");
            Console.ReadLine();
        }

        protected virtual TRequestType GetBaseRequestFromRow(MultiFileImportDataSet multiFileImportDataSet, IParentChildMapAdder<TRequestType,TResponseType> mapper)
        {
            var existing = mapper.CheckEntityExistsInQbo(multiFileImportDataSet);
            var newRequest = new TRequestType()
            {
                EntityTypeName = ImportType.ToString(),
                RequestType = existing != null ? RequestType.Update : RequestType.Create,
                SyncToken = existing != null ? existing.SyncToken : null,
                Id = existing != null ? existing.EntityId : null
            };
            newRequest = mapper.AddMappings(multiFileImportDataSet, newRequest);
            return newRequest;
        }
    }

    internal class MultiFileImportDataSet
    {
        public DataRow ParentRow { get; set; }
        public List<DataRow> DetailRows { get; set; }

        /// <summary>
        /// Populate this with additional data from parent columns that might help in the mapping step of an import package
        /// </summary>
        public Dictionary<string,string> OptionalCorrelator { get; set; }
    }
}
