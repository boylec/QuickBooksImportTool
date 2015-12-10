using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using DLI.Core.Common.DTOs.QuickBooksOnline;
using DLI.Core.Common.Enums.QuickBooksOnline;
using DLI.Core.Common.Requests.QuickBooksOnline;
using DLI.Core.Common.Responses.QuickBooksOnline;
using QboImporterTool.Classes.Bases;
using QboImporterTool.Mapper;
using RestSharp;

namespace QboImporterTool.Classes
{
    internal class BillImportPackage :
        MultiFileImportPackage<SaveQuickBooksOnlineBillRequest, QuickBooksOnlineBillResponse>
    {
        //protected List<BillImportPackage> BillImports { get; set; }
        protected new BillMapper Mapper;

        public BillImportPackage(string billFile, string billDetailsFile) : base(billFile, billDetailsFile, new BillMapper(), ImportableTypes.Bill)
        {
            FilePath = billFile;
            RawDataSet = Utils.GetRowsFromExcelFile(billFile);
            Mapper = new BillMapper();
            PayLoad = new List<QbOnlineBatchItemRequest>();
            TypeOfImport = typeof (SaveQuickBooksOnlineBillRequest);
            ItemChoiceType = ItemChoiceType6.Bill;
            ImportType = ImportableTypes.Bill;
            DetailDataSet = Utils.GetRowsFromExcelFile(billDetailsFile);
        }

        public override void ExtractRequestsFromRows()
        {
            var totalInvoices = RawDataSet.Count;
            Console.WriteLine("Extracting requests from " + RawDataSet.Count + "rows for {0} entities.", ImportType.ToString());
            Console.WriteLine("With a total " + DetailDataSet.Count + " detail items to package.");
            var vendorName = "";
            for (var x = 0; x < RawDataSet.Count; x++)
            {
                Console.Write("\r" + ImportType + ": " + (x + 1) + "/ " + totalInvoices);

                var row = RawDataSet[x];

                //If the parentRow is the Total line then it won't be valid.
                if (!Mapper.IsParentRowValid(row)) continue;

                //If it is not the total line, but there is something in the seperator column, that something is the vendor name
                //Then skip this parentRow with continue because it isn't the parent parentRow. So we just got the customer name is all.
                if (!(row["Seperator"] is DBNull))
                {
                    vendorName = row["Seperator"].ToString();
                    continue;
                }

                //At this point this parentRow must be the parent parentRow
                var parentItem = new MultiFileImportDataSet
                {
                    ParentRow = row,
                    OptionalCorrelator = new Dictionary<string, string> {{"Vendor", vendorName}}
                };


                parentItem.DetailRows = Mapper.GetDetailRows(row, DetailDataSet, parentItem);

                var requestEntity = GetBaseRequestFromRow(parentItem, Mapper);

                PayLoad.Add(new QbOnlineBatchItemRequest()
                {
                    Entity = requestEntity,
                    BatchId = x.ToString(),
                    OperationType = (OperationEnum)(int)requestEntity.RequestType,
                    ItemElementName = ItemChoiceType
                });

                vendorName = "";
            }
            Console.WriteLine();
            Console.WriteLine("Complete! Processing entities sent to QBO sometimes takes awhile...");
        }
    }

    internal class BillMapper :
        IParentChildMapAdder<SaveQuickBooksOnlineBillRequest, QuickBooksOnlineBillResponse>
    {
        public List<QbOnlineBillLineItem> MapLineItems(List<DataRow> billDetailRows)
        {
            //Return all of the lines for this particular bill.
            return billDetailRows.Select(row => new QbOnlineBillLineItem()
            {
                Amount = Convert.ToDecimal(row["Debit"].ToString()),
                IsSubTotalLine = false,
                BillableStatus = "HasBeenBilled",        //Billed, NonBillable, or HasBeenBilled
                ExpenseAccountId = Program.CurrentAccounts.Find(x => x.Name == row["Account"].ToString()).EntityId
            }).ToList();
        }

        public QuickBooksOnlineBillResponse CheckEntityExistsInQbo(MultiFileImportDataSet dataSet)
        {
            var billDocNumber = dataSet.ParentRow["Num"].ToString();
            DateTime? optionalDate = null;
            var vendorName = dataSet.OptionalCorrelator.ContainsKey("Vendor")
                ? dataSet.OptionalCorrelator["Vendor"]
                : null;
            
            if (dataSet.OptionalCorrelator.ContainsKey("Date"))
                optionalDate = DateTime.Parse(dataSet.OptionalCorrelator["Date"]);

            if (Program.CurrentBills.Any(x => x.BillNumber == billDocNumber))
                return Program.CurrentBills.Find(x => x.BillNumber == billDocNumber);

            if (vendorName == null || optionalDate == null) return null;
            
            var vendorId = Program.CurrentCustomers.Find(x => x.DisplayName == vendorName).EntityId;
            
            return Program.CurrentBills.Find(x => x.CreatedDate == optionalDate && x.VendorId == vendorId);
        }

        /// <summary>
        /// This one is a little weird because we are getting the vendor name from the parent file, so we are telling
        /// the package the row is good even if it has no data besides the customer name. Then in the
        /// ExtractRows overriden function for this package it determines that this is just the customer name and skips the row
        /// while taking the customer name from that for the subsequent rows... CONFUSING!
        /// </summary>
        /// <param name="row"></param>
        /// <returns></returns>
        public bool IsParentRowValid(DataRow row)
        {
            var separatorColumnValue = row["Seperator"].ToString();
            return !separatorColumnValue.StartsWith("Total");
        }

        public SaveQuickBooksOnlineBillRequest AddMappings(
            MultiFileImportDataSet billItem, SaveQuickBooksOnlineBillRequest baseRequest)
        {
            baseRequest.BillNumber = billItem.ParentRow["Num"].ToString();
            baseRequest.CreatedDate = DateTime.Parse(billItem.ParentRow["Date"].ToString());
            baseRequest.DueDate = DateTime.Parse(billItem.ParentRow["Due Date"].ToString());
            baseRequest.LineItems = MapLineItems(billItem.DetailRows);
            baseRequest.VendorId =
                Program.CurrentCustomers.Find(x => x.DisplayName == billItem.OptionalCorrelator["Vendor"]).EntityId;
            return baseRequest;
        }

        public List<DataRow> GetDetailRows(DataRow parentRow, DataRowCollection detailSet,
            MultiFileImportDataSet parentItem)
        {
            var billVendorName = parentItem.OptionalCorrelator["Vendor"];
            DateTime? billDate;
            if(parentItem.OptionalCorrelator.ContainsKey("Date"))
                billDate = DateTime.Parse(parentItem.OptionalCorrelator["Date"]);
            else
            {
                billDate = null;
            }

            var billNumber = parentRow["Num"].ToString();
            parentItem.DetailRows = new List<DataRow>();
            for (var y = 0; y < detailSet.Count; y++)
            {
                var byNumber = false;
                var byVendorNameAndDate = false;
                if (detailSet[y]["Type"].ToString() != "Bill")
                    continue;
                if (!(detailSet[y]["Num"] is DBNull)
                    && detailSet[y]["Num"].ToString() == billNumber)
                {
                    byNumber = true;
                }
                else if (billDate != null &&
                         billVendorName != null &&
                         !(detailSet[y]["Name"] is DBNull) &&
                         !(detailSet[y]["Date"] is DBNull) &&
                         detailSet[y]["Name"].ToString() == billVendorName &&
                         billDate == DateTime.Parse(detailSet[y]["Date"].ToString()))
                {
                    byVendorNameAndDate = true;
                }
                else
                {
                    continue;
                }

                //Move to the next parentRow
                y++;

                if (byNumber)
                {
                    while (detailSet[y]["Num"].ToString() == billNumber)
                    {
                        parentItem.DetailRows.Add(detailSet[y]);
                        y++;
                    }
                }
                else if (byVendorNameAndDate)
                {
                    while (DateTime.Parse(detailSet[y]["Date"].ToString()) == billDate.Value &&
                           detailSet[y]["Name"].ToString() == billVendorName)
                    {
                        parentItem.DetailRows.Add(detailSet[y]);
                        y++;
                    }
                }
            }
            return parentItem.DetailRows;
        }
    }
}

