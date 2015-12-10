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
        //TRequestType AddMappings(DataRow row, TRequestType request);
        TResponseType GetExistingFromRow(DataRow row);
    }
}
