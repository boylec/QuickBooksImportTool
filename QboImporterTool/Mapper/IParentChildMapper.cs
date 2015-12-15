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

        /// <summary>
        /// Looks at a row to determine is it can be considered a "Parent" data row.
        /// Typically this would be occuring ina loop which examines the excel file from top to bottom sequentially.
        /// This should be logic based on the data within each column to determine if this is a valid parent row.
        /// </summary>
        /// <param name="row">The row to be examined</param>
        /// <returns>Whether or not the DataRow is a parent row for this import package.</returns>
        bool IsParentRowValid(DataRow row);

        /// <summary>
        /// Determines whether a particular row can be considered a detail for the given parent row.
        /// </summary>
        /// <param name="detailRow">The row to be examined.</param>
        /// <param name="parentRowForDetail">The parent row for the row being examined.</param>
        /// <returns>Whether or not the detail row is a valid detail fro the given parent row.</returns>
        bool IsDetailRowValid(DataRow detailRow, DataRow parentRowForDetail);
        List<DataRow> GetDetailRows(DataRow parentRow, DataRowCollection detailSet, MultiFileImportDataSet parentItem);
    }
}
