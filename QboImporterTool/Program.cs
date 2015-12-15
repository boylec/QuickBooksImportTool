using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using Excel;
using DLI.Core.Common;
using DLI.Core.Common.DTOs.QuickBooksOnline;
using DLI.Core.Common.Enums.QuickBooksOnline;
using DLI.Core.Common.Requests.QuickBooksOnline;
using DLI.Core.Common.Responses.QuickBooksOnline;
using QboImporterTool.Mapper;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using QboImporterTool.Classes;
using QboImporterTool.Classes.Bases;
using RestSharp;
using RestSharp.Deserializers;
using RestSharp.Extensions;

namespace QboImporterTool
{
    public enum ImportableTypes
    {
        Customer = 1,
        Vendor,
        Account,
        Employee,
        Item,
        Invoice,
        Bill,
        Term
    }

    public enum RefreshableTypes
    {
        Customer = 1,
        Vendor,
        Account,
        Employee,
        Term,
        Item,
        Invoice,
        Preferences,
        Bill
    }
    class Program
    {
        public static readonly string QboIntegrationDomain = ConfigurationManager.AppSettings.Get("DLI.Web.Invoice.Domain");
        public static readonly List<QbOnlineBatchItemRequest> ListOfBatchItemRequests = new List<QbOnlineBatchItemRequest>();
        internal static List<QuickBooksOnlineCustomerResponse> CurrentCustomers = new List<QuickBooksOnlineCustomerResponse>();
        internal static List<QuickBooksOnlineTermResponse> CurrentTerms = new List<QuickBooksOnlineTermResponse>();
        internal static List<QuickBooksOnlineVendorResponse> CurrentVendors = new List<QuickBooksOnlineVendorResponse>();
        internal static List<QuickBooksOnlineAccountResponse> CurrentAccounts = new List<QuickBooksOnlineAccountResponse>();
        internal static List<QuickBooksOnlineEmployeeResponse> CurrentEmployees = new List<QuickBooksOnlineEmployeeResponse>();
        internal static List<QuickBooksOnlineItemResponse> CurrentItems = new List<QuickBooksOnlineItemResponse>();
        internal static List<QuickBooksOnlineInvoiceResponse> CurrentInvoices = new List<QuickBooksOnlineInvoiceResponse>();
        internal static List<QuickBooksOnlinePreferencesResponse> CurrentPreferences = new List<QuickBooksOnlinePreferencesResponse>();
        internal static List<QuickBooksOnlineBillResponse> CurrentBills = new List<QuickBooksOnlineBillResponse>();
        internal static List<QuickBooksOnlineBillPaymentResponse> CurrentBillPayments = new List<QuickBooksOnlineBillPaymentResponse>(); 

        private static void RefreshQboData()
        {
            Console.WriteLine("Refreshing all current QBO data from QBO");
            CurrentCustomers = Utils.GetAllEntities<QuickBooksOnlineCustomerResponse>("Customer");
            CurrentTerms = Utils.GetAllEntities<QuickBooksOnlineTermResponse>("Term");
            CurrentAccounts = Utils.GetAllEntities<QuickBooksOnlineAccountResponse>("Account");
            CurrentEmployees = Utils.GetAllEntities<QuickBooksOnlineEmployeeResponse>("Employee");
            CurrentVendors = Utils.GetAllEntities<QuickBooksOnlineVendorResponse>("Vendor");
            CurrentItems = Utils.GetAllEntities<QuickBooksOnlineItemResponse>("Item");
            CurrentInvoices = Utils.GetAllEntities<QuickBooksOnlineInvoiceResponse>("Invoice");
            CurrentPreferences = Utils.GetAllEntities<QuickBooksOnlinePreferencesResponse>("Preferences");
            CurrentBills = Utils.GetAllEntities<QuickBooksOnlineBillResponse>("Bill");
        }
        private static void Main(string[] args)
        {
            Logger.Instance.Log("Program Began");
            Console.SetWindowSize(170,45);
            var importerList = new List<IImporter>()
            {
                new CustomerImportPackage("customers.xlsx"),
                new VendorImportPackage("vendors.xlsx"),
                new AccountImportPackage("accounts.xlsx"),
                new EmployeeImportPackage("employees.xlsx"),
                new ItemImportPackage("items.xlsx"),
                new InvoiceImportPackage("invoices.xlsx"),
                new BillImportPackage("bills.xlsx"),
                new TermImportPackage("terms.xlsx")
            };


            while (true)
            {
                RefreshQboData();
                ListOfBatchItemRequests.Clear();
                var typeToImport = ShowMainMenu();
                
                var packageSelected = importerList.Find(x => x.ImportType == typeToImport);
                packageSelected.ExtractRequestsFromRows();
                packageSelected.SendPayloadToQbo();
            }
        }

        private static ImportableTypes ShowMainMenu()
        {
            Console.Clear();
            var optionNumber = 1;
            Console.WriteLine("Select the entity to import:");
            var options = Enum.GetNames(typeof (ImportableTypes));
            var maxSelection = options.Length;

            foreach (var type in options)
            {
                Console.WriteLine("{0} - {1}s", optionNumber, type);
                optionNumber++;
            }
            var optionSelected = 0;
            var intWasSelected = false;

            do
            {
                 var charSelected = Console.ReadKey().KeyChar.ToString();
                intWasSelected = int.TryParse(charSelected, out optionSelected);
            } while (intWasSelected == false || optionSelected <= 0 || optionSelected > maxSelection);

            Console.WriteLine();
            return (ImportableTypes)optionSelected;
        }
    }
}
