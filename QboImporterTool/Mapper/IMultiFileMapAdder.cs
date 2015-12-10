using System.Collections.Generic;
using System.Data;
using System.Runtime.Remoting.Channels;
using DLI.Core.Common.Requests.QuickBooksOnline;
using QboImporterTool.Classes.Bases;

namespace QboImporterTool.Mapper
{
    interface IMultiFileMapAdder<TRequestType>
        where TRequestType : QbOnlineRequest, new()
    {
        /// <summary>
        /// Adds mappings to a baseRequest including all parent and detail objects
        /// </summary>
        /// <param name="multiFileDataSet">Set that contains datarows for Parent and Details of the item</param>
        /// <param name="baseRequest">The base baseRequest to be filled with the converted information</param>
        /// <returns></returns>
        TRequestType AddMappings(MultiFileImportDataSet multiFileDataSet, TRequestType baseRequest);
    }
}
