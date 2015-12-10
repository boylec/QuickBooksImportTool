using System.Collections.Generic;
using System.Data;
using System.Runtime.Remoting.Channels;
using DLI.Core.Common.Requests.QuickBooksOnline;
using DLI.Core.Common.Responses.QuickBooksOnline;
using QboImporterTool.Classes.Bases;

namespace QboImporterTool.Mapper
{
    interface IParentChildMapAdder<TRequestType, out TResponseType> :
        IMultiFileMapAdder<TRequestType>
        where TRequestType : QbOnlineRequest, new()
        where TResponseType : QbOnlineResponse, new()
    {
        TResponseType CheckEntityExistsInQbo(MultiFileImportDataSet dataSet);
        bool IsParentRowValid(DataRow row);
        List<DataRow> GetDetailRows(DataRow parentRow, DataRowCollection detailSet, MultiFileImportDataSet parentItem);
    }
}
