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
            var mainPhone = Utils.FormatPhone(row["Main Phone"].ToString());
            var altPhone = Utils.FormatPhone(row["Alt. Phone"].ToString());

            request.AltPhone = altPhone;
            if (row["Bill to 1"].ToString() == row["Company"].ToString())
            {
                request.BillingAddress1 = row["Bill to 2"].ToString();
                request.BillingAddress2 = row["Bill to 3"].ToString();
                request.BillingAddress3 = row["Bill to 4"].ToString();
                request.BillingAddress4 = row["Bill to 5"].ToString();
            }
            else
            {
                request.BillingAddress1 = row["Bill to 1"].ToString();
                request.BillingAddress2 = row["Bill to 2"].ToString();
                request.BillingAddress3 = row["Bill to 3"].ToString();
                request.BillingAddress4 = row["Bill to 4"].ToString();
                request.BillingAddress5 = row["Bill to 5"].ToString();
            }
            request.CompanyName = row["Company"].ToString();
            request.ContactName = row["Primary Contact"].ToString();
            request.DisplayName = customerName;
            request.Email = row["Main Email"].ToString();
            request.FaxNumber = row["Fax"].ToString();
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
