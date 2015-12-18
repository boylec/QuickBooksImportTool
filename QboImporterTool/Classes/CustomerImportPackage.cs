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
    internal class CustomerImportPackage :
        BaseImportPackage<SaveQuickBooksOnlineCustomerRequest, QuickBooksOnlineCustomerResponse>
    {
        public CustomerImportPackage(string fileName) : base(fileName, new CustomerMapper(), ImportableTypes.Customer)
        {

        }
    }

    internal class CustomerMapper : IRowToRequestMapper<SaveQuickBooksOnlineCustomerRequest, QuickBooksOnlineCustomerResponse>
    {

        public SaveQuickBooksOnlineCustomerRequest AddMappingsToBaseRequest(DataRow row,
            SaveQuickBooksOnlineCustomerRequest request)
        {
            var customerName = row["Customer"].ToString();
            var mainPhone = Utils.FormatPhone(row["Main Phone"].ToString().Replace(".", "-").Replace("/", "-"));
            var altPhone = Utils.FormatPhone(row["Alt. Phone"].ToString().Replace(".", "-").Replace("/", "-"));

            request.AltPhone = altPhone;
            request.BillingAddress1 = row["Street1"].ToString();
            request.BillingAddress2 = row["Street2"].ToString();
            request.BillingCity = row["City"].ToString();
            request.BillingState = row["State"].ToString();
            request.BillingZip = row["Zip"].ToString();
            request.CompanyName = row["Company"].ToString();
            request.ContactName = row["Primary Contact"].ToString();
            request.DisplayName = customerName;
            request.Email = row["Main Email"].ToString();
            request.FaxNumber = row["Fax"].ToString().Replace(".", "-").Replace("/", "-");
            request.Organization = true;
            request.TelephoneNumber = mainPhone;
            request.OpeningBalance = 0;//Convert.ToDecimal(row["Balance Total"]);
            request.OpeningBalanceDate = null;//DateTime.Now;
            request.TermId = Program.CurrentTerms.Find(x => x.TermName == "Net 30").EntityId;
            return request;
        }

        public QuickBooksOnlineCustomerResponse GetExistingFromRow(DataRow row)
        {
            var customerName = row["Customer"].ToString();
            return Program.CurrentCustomers.Find(x => x.DisplayName == customerName.ToString());

        }
    }
}
