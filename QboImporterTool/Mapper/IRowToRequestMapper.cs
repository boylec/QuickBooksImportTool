using System.Data;
using System.Runtime.Remoting.Channels;
using DLI.Core.Common.Requests.QuickBooksOnline;
using DLI.Core.Common.Responses.QuickBooksOnline;

namespace QboImporterTool.Mapper
{
    interface IRowToRequestMapper<TRequestType, out TResponseType> :
        ISingleFileMapAdder<TRequestType>
        where TRequestType : QbOnlineRequest, new()
        where TResponseType : QbOnlineResponse, new()
    {
        /// <summary>
        /// Searches the current entities within QBO to see if one exists based on the DataRow.
        /// </summary>
        /// <param name="row">The row of information which will determine whether the entity already exists within QBO</param>
        /// <returns>The existing entity or NULL</returns>
        TResponseType GetExistingFromRow(DataRow row);
    }
}
