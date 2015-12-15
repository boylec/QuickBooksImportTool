using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DLI.Core.Common.Enums.QuickBooksOnline;
using DLI.Core.Common.Requests.QuickBooksOnline;
using DLI.Core.Common.Responses.QuickBooksOnline;
using QboImporterTool.Classes.Bases;
using QboImporterTool.Mapper;

namespace QboImporterTool.Classes
{
    internal class VendorImportPackage :
        BaseImportPackage<SaveQuickBooksOnlineVendorRequest, QuickBooksOnlineVendorResponse>
    {
        public VendorImportPackage(string fileName)
            : base(fileName, new VendorMapper(), ImportableTypes.Vendor)
        {

        }
    }

    internal class VendorMapper : IRowToRequestMapper<SaveQuickBooksOnlineVendorRequest, QuickBooksOnlineVendorResponse>
    {
        /// <summary>
        /// Sets the request based on the information in the row.
        /// </summary>
        /// <param name="row">The row containing information for the entity request</param>
        /// <param name="vendorRequest">The request itself</param>
        /// <returns>The request filled in with information from the row</returns>
        public SaveQuickBooksOnlineVendorRequest AddMappingsToBaseRequest(DataRow row,
            SaveQuickBooksOnlineVendorRequest vendorRequest)
        {
            //vendorRequest.Active = row["Active Status"].ToString() == "Active";
            vendorRequest.DisplayName = row["Vendor"].ToString();
            vendorRequest.OpeningBalance = 0;//Convert.ToDecimal(row["Balance"]);
            vendorRequest.OpeningBalanceDate = null;//DateTime.Now;
            vendorRequest.CompanyName = row["Company"].ToString();
            vendorRequest.BillFrom1 = row["Bill from 1"].ToString();
            vendorRequest.BillFrom2 = row["Bill from 2"].ToString();
            vendorRequest.BillFrom3 = row["Bill from 3"].ToString();
            vendorRequest.BillFrom4 = row["Bill from 4"].ToString();
            vendorRequest.BillFrom5 = row["Bill from 5"].ToString();
            vendorRequest.PrimaryContact = row["Primary Contact"].ToString();
            vendorRequest.MainPhone = Utils.FormatPhone(row["Main Phone"].ToString());
            vendorRequest.Fax = row["Fax"].ToString().Replace("/", "-");
            vendorRequest.AltPhone = Utils.FormatPhone(row["Alt. Phone"].ToString());
            vendorRequest.SecondaryContact = row["Secondary Contact"].ToString();
            return vendorRequest;
        }

        public QuickBooksOnlineVendorResponse GetExistingFromRow(DataRow row)
        {
            var vendorName = row["Vendor"].ToString();
            return Program.CurrentVendors.Find(x => x.DisplayName == vendorName);
        }
    }
}
