using System;
using System.Collections.Generic;
using System.Data;
using DLI.Core.Common.Enums.QuickBooksOnline;
using DLI.Core.Common.Requests.QuickBooksOnline;
using DLI.Core.Common.Responses.QuickBooksOnline;

namespace ExcelToQBOCustomer.Abstract
{
    internal abstract class ImportPackage<TRequestType,TResponseType> where TRequestType : QbOnlineRequest, new() where TResponseType : QbOnlineResponse, new()
    {
        //public abstract TRequestType AddMappingsToBaseRequest(DataRow row,TRequestType baseRequest);
        public abstract void ExtractRequestsFromRows();
        protected Func<object[],TResponseType> ExistsCheck;
        public abstract TResponseType Exists(params object[] args);
        internal ItemChoiceType6 ItemChoiceType { get; set; }
        internal Type TypeOfImport { get; set; }
        internal string FilePath { get; set; }
        protected DataRowCollection RawDataSet { get; set; }
        internal List<QbOnlineBatchItemRequest> PayLoad { get; set; }

        protected ImportPackage(string filePath, Func<dynamic, TResponseType> existCheck)
        {
            FilePath = filePath;
            RawDataSet = Utils.GetRowsFromExcelFile(filePath);
            ExistsCheck = existCheck;

            PayLoad = new List<QbOnlineBatchItemRequest>();
            TypeOfImport = typeof(TRequestType);
            ItemChoiceType = (ItemChoiceType6)Enum.Parse(typeof(ItemChoiceType6), TypeOfImport.Name);
        }
    }
}
