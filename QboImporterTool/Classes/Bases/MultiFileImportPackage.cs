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
}
