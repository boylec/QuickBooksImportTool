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
        /// <summary>
        /// Sets the request based on the information in the row.
        /// </summary>
        /// <param name="row">The row containing information for the entity request</param>
        /// <param name="request">The request itself</param>
        /// <returns>The request filled in with information from the row</returns>
        TRequestType AddMappingsToBaseRequest(DataRow row, TRequestType request);
    }
}
