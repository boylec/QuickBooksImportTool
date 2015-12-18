using System;
using System.Collections.Generic;
using System.Configuration;
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
            vendorRequest.BillFrom1 = row["Bill from Street 1"].ToString();
            vendorRequest.BillFrom2 = row["Bill from Street 2"].ToString();
            vendorRequest.BillCity = row["Bill from City"].ToString();
            vendorRequest.BillState = row["Bill from State"].ToString();
            vendorRequest.BillZip = row["Bill from Zip"].ToString();
            vendorRequest.PrimaryContact = row["Primary Contact"].ToString();
            vendorRequest.MainPhone = Utils.FormatPhone(row["Main Phone"].ToString().Replace(".","-").Replace("/","-"));
            vendorRequest.Fax = row["Fax"].ToString().Replace(".", "-").Replace("/", "-");
            vendorRequest.AltPhone = Utils.FormatPhone(row["Alt. Phone"].ToString().Replace(".", "-").Replace("/", "-"));
            vendorRequest.SecondaryContact = row["Secondary Contact"].ToString();
            vendorRequest.TaxId = !(row["Tax ID"] is DBNull) ? row["Tax ID"].ToString() : null;
            vendorRequest.Vendor1099 = !string.IsNullOrEmpty(vendorRequest.TaxId);
            vendorRequest.TermsId = !(row["Terms"] is DBNull)
                ? Program.CurrentTerms.Find(x => x.TermName == row["Terms"].ToString()).EntityId
                : null;
            return vendorRequest;
        }

        public QuickBooksOnlineVendorResponse GetExistingFromRow(DataRow row)
        {
            var vendorName = row["Vendor"].ToString();
            return Program.CurrentVendors.Find(x => x.DisplayName == vendorName);
        }
    }
}
