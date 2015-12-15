using System;
using System.Collections.Generic;
using System.Data;
using DLI.Core.Common.Enums.QuickBooksOnline;
using DLI.Core.Common.Requests.QuickBooksOnline;
using DLI.Core.Common.Responses.QuickBooksOnline;
using QboImporterTool.Mapper;

namespace QboImporterTool.Classes.Bases
{
    internal class SplitLinesImportPackage<TRequestType, TResponseType> : BaseImportPackage<TRequestType, TResponseType>
        where TRequestType : QbOnlineRequest, new() where TResponseType : QbOnlineResponse, new()
    {
        public new IParentChildMapAdder<TRequestType, TResponseType> Mapper;
        
        public SplitLinesImportPackage(string fileName, IParentChildMapAdder<TRequestType,TResponseType> mapper, ImportableTypes importType) : base(fileName, importType)
        {
            Mapper = mapper;
        }

        public override void ExtractRequestsFromRows()
        {
            var totalInvoices = RawDataSet.Count;
            Console.WriteLine("Extracting requests from " + RawDataSet.Count + " rows for {0} entities.", ImportType.ToString());
            var parentCount = 0;
            var detailCount = 0;
            int x;
            for (x = 0; x < RawDataSet.Count; x++)
            {
                Console.Write("\rRow: " + (x + 1) + "/ " + totalInvoices + ", Parent " + ImportType + "'s Extracted: " + parentCount + ", " + ImportType + " Details Extracted: " + detailCount);

                var row = RawDataSet[x];
                if (!Mapper.IsParentRowValid(row)) continue;

                var parentItem = new MultiFileImportDataSet { ParentRow = row };
                var parentBatchId = x;
                parentCount++;
                
                x++;
                while (x < RawDataSet.Count && Mapper.IsDetailRowValid(RawDataSet[x], parentItem.ParentRow))
                {
                    parentItem.DetailRows.Add(RawDataSet[x]);
                    x++;
                    detailCount++;
                }
                x--;

                var requestEntity = GetBaseRequestFromRow(parentItem);

                PayLoad.Add(new QbOnlineBatchItemRequest()
                {
                    Entity = requestEntity,
                    BatchId = parentBatchId.ToString(),
                    OperationType = (OperationEnum)(int)requestEntity.RequestType,
                    ItemElementName = ItemChoiceType
                });
            }
            Console.Write("\rRow: " + x + "/ " + totalInvoices + ", Parent " + ImportType + "'s Extracted: " + parentCount + ", " + ImportType + " Details Extracted: " + detailCount);
            Console.WriteLine();
            Console.WriteLine("Complete! Processing entities sent to QBO sometimes takes awhile...");
        }

        protected virtual TRequestType GetBaseRequestFromRow(MultiFileImportDataSet multiFileImportDataSet)
        {
            var existing = Mapper.CheckEntityExistsInQbo(multiFileImportDataSet);
            var newRequest = new TRequestType()
            {
                EntityTypeName = ImportType.ToString(),
                RequestType = existing != null ? RequestType.Update : RequestType.Create,
                SyncToken = existing != null ? existing.SyncToken : null,
                Id = existing != null ? existing.EntityId : null
            };
            newRequest = Mapper.AddMappings(multiFileImportDataSet, newRequest);
            return newRequest;
        }
        
    }
    internal class MultiFileImportDataSet
    {
        public MultiFileImportDataSet()
        {
            DetailRows = new List<DataRow>();
        }

        public DataRow ParentRow { get; set; }
        public List<DataRow> DetailRows { get; set; }

        /// <summary>
        /// Populate this with additional data from parent columns that might help in the mapping step of an import package
        /// </summary>
        public Dictionary<string, string> OptionalCorrelator { get; set; }
    }
}
