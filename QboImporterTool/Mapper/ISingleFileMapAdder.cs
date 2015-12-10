using System.Collections.Generic;
using System.Data;
using System.Runtime.Remoting.Channels;
using DLI.Core.Common.Requests.QuickBooksOnline;
using QboImporterTool.Classes.Bases;

namespace QboImporterTool.Mapper
{
    interface ISingleFileMapAdder<TRequestType>
        where TRequestType : QbOnlineRequest, new()
    {
        TRequestType AddMappingsToBaseRequest(DataRow row, TRequestType request);
    }
}
