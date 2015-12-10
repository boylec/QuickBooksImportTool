﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using DLI.Core.Common.Enums.QuickBooksOnline;
using DLI.Core.Common.Requests.QuickBooksOnline;
using DLI.Core.Common.Responses.QuickBooksOnline;
using ICSharpCode.SharpZipLib.Zip;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using QboImporterTool.Mapper;
using RestSharp;

namespace QboImporterTool.Classes
{
    internal interface IImporter
    {
        ImportableTypes ImportType { get; set; }
        ItemChoiceType6 ItemChoiceType { get; set; }
        List<QbOnlineBatchItemRequest> PayLoad { get; set; }
        void ExtractRequestsFromRows();
        void SendPayloadToQbo();
        bool WaitingForResponse { get; set; }
        decimal TimeTaken { get; set; }
    }

    internal class BaseImportPackage<TRequestType,TResponseType> : IImporter where TRequestType : QbOnlineRequest, new() where TResponseType : QbOnlineResponse, new()
    {
        public bool WaitingForResponse { get; set; }
        public decimal TimeTaken { get; set; }
        //public abstract TRequestType AddMappings(DataRow row,TRequestType baseRequest);
        public ImportableTypes ImportType { get; set; }
        public ItemChoiceType6 ItemChoiceType { get; set; }
        internal Type TypeOfImport { get; set; }
        internal string FilePath { get; set; }
        internal IRowToRequestMapper<TRequestType, TResponseType> Mapper; 
        protected DataRowCollection RawDataSet { get; set; }
        public List<QbOnlineBatchItemRequest> PayLoad { get; set; }

        protected internal BaseImportPackage(string filePath, IRowToRequestMapper<TRequestType, TResponseType> mapper, ImportableTypes importType)
        {
            FilePath = filePath;
            RawDataSet = Utils.GetRowsFromExcelFile(filePath);
            Mapper = mapper;
            PayLoad = new List<QbOnlineBatchItemRequest>();
            TypeOfImport = typeof(TRequestType);
            ItemChoiceType = (ItemChoiceType6)Enum.Parse(typeof(ItemChoiceType6), importType.ToString());
            ImportType = importType;
        }

        protected internal BaseImportPackage()
        {
            
        }

        protected virtual TRequestType GetBaseRequestFromRow(DataRow row, IRowToRequestMapper<TRequestType,TResponseType> mapper)
        {
            var existing = mapper.GetExistingFromRow(row);
            var newRequest = new TRequestType()
            {
                EntityTypeName = ImportType.ToString(),
                RequestType = existing != null ? RequestType.Update : RequestType.Create,
                SyncToken = existing != null ? existing.SyncToken : null,
                Id = existing != null ? existing.EntityId : null
            };
            newRequest = mapper.AddMappingsToBaseRequest(row, newRequest);
            return newRequest;
        }

        public virtual void ExtractRequestsFromRows()
        {
            for(var x = 0; x < RawDataSet.Count; x++)
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
        }

        public virtual void SendPayloadToQbo()
        {
            Console.WriteLine(@"Sending Batch Request with all packaged " + ItemChoiceType.ToString() + " save requests to QBO...");
            var restClient = new RestClient(Program.QboIntegrationDomain) { Timeout = 300000000 };
            var request = new RestRequest("api/QuickBooks/Batch/DoBatch", Method.POST);
            request.AddJsonBody(PayLoad);
            request.AddHeader("authToken", "db55d34e-eacd-42cb-b1ba-48f724b35103");
            Console.WriteLine(@"Sent...");
            Console.Write(@"Awaiting Response...");
            restClient.ExecuteAsync(request, OnResponseReceived);
            WaitingForResponse = true;
            TimeTaken = 0;

            while (WaitingForResponse)
            {
                System.Threading.Thread.Sleep(250);
                TimeTaken += 250;
                Console.Write(@".");
            }
            Console.WriteLine();
            Console.WriteLine(@"Import operation(s) completed for {0}s...", ImportType.ToString());
            Console.WriteLine(@"Total time taken: {0} seconds...", TimeTaken / 1000);
            Console.WriteLine(@"Press ENTER to continue.");
            Console.ReadLine();
        }

        public virtual void OnResponseReceived(IRestResponse response)
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
}
