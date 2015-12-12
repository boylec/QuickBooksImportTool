﻿using System;
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
        public BillImportPackage(string billFile, string billDetailsFile) : base(billFile, billDetailsFile, new BillMapper(), ImportableTypes.Bill)
        {
            //FilePath = billFile;
            //RawDataSet = Utils.GetRowsFromExcelFile(billFile);
            //Mapper = new BillMapper();
            //PayLoad = new List<QbOnlineBatchItemRequest>();
            //TypeOfImport = typeof (SaveQuickBooksOnlineBillRequest);
            //ItemChoiceType = ItemChoiceType6.Bill;
            //ImportType = ImportableTypes.Bill;
            //DetailDataSet = Utils.GetRowsFromExcelFile(billDetailsFile);
        }

        public override void ExtractRequestsFromRows()
        {
            var totalInvoices = RawDataSet.Count;
            Console.WriteLine("Extracting requests from " + RawDataSet.Count + " rows for {0} entities.", ImportType.ToString());
            Console.WriteLine("With a total " + DetailDataSet.Count + " detail items to package.");
            var vendorName = "";
            for (var x = 0; x < RawDataSet.Count; x++)
            {
                Console.Write("\r" + ImportType + ": " + (x + 1) + "/ " + totalInvoices);

                var row = RawDataSet[x];

                //If the parentRow is the Total line then it won't be valid.
                if (!Mapper.IsParentRowValid(row))
                {
                    if (row["Seperator"].ToString().StartsWith("Total"))
                        vendorName = "";
                    continue;
                }

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
                    OptionalCorrelator = new Dictionary<string, string> {{"Vendor", vendorName}, {"Date",row["Date"].ToString()}, {"Amount",row["Open Balance"].ToString()}}
                };


                parentItem.DetailRows = Mapper.GetDetailRows(row, DetailDataSet, parentItem);

                if (parentItem.DetailRows.Count == 0) continue;
                
                var requestEntity = GetBaseRequestFromRow(parentItem, Mapper);

                PayLoad.Add(new QbOnlineBatchItemRequest()
                {
                    Entity = requestEntity,
                    BatchId = x.ToString(),
                    OperationType = (OperationEnum) (int) requestEntity.RequestType,
                    ItemElementName = ItemChoiceType
                });
            }
            Console.WriteLine();
            Console.WriteLine("Complete! Processing entities sent to QBO sometimes takes awhile...");
        }
    }

    internal class BillMapper :
        IParentChildMapAdder<SaveQuickBooksOnlineBillRequest, QuickBooksOnlineBillResponse>
    {
        public List<QbOnlineBillLineItem> MapLineItems(MultiFileImportDataSet billDetailRows)
        {
            return billDetailRows.DetailRows.Select(row => new QbOnlineBillLineItem
            {
                Amount = !(row["Debit"] is DBNull) ? Convert.ToDecimal(row["Debit"].ToString()) : -Convert.ToDecimal(row["Credit"].ToString()),
                IsSubTotalLine = false,
                BillableStatus = "NotBillable",
                ExpenseAccountId = Program.CurrentAccounts.Find(x => x.Name == row["Account"].ToString()).EntityId
            }).ToList();
        }

        public QuickBooksOnlineBillResponse CheckEntityExistsInQbo(MultiFileImportDataSet dataSet)
        {
            //Check by bill number
            var billDocNumber = dataSet.ParentRow["Num"].ToString();
            
            //Check here
            if (Program.CurrentBills.Any(x => x.BillNumber == billDocNumber))
                return Program.CurrentBills.Find(x => x.BillNumber == billDocNumber);

            //If not we check by date, vendor name, and amount
            DateTime? optionalDate = null;
            var vendorName = dataSet.OptionalCorrelator.ContainsKey("Vendor")
                ? dataSet.OptionalCorrelator["Vendor"]
                : null;
            var amount = dataSet.OptionalCorrelator.ContainsKey("Amount")
                ? Convert.ToDecimal(dataSet.OptionalCorrelator["Amount"])
                : 0;

            if (dataSet.OptionalCorrelator.ContainsKey("Date") && dataSet.OptionalCorrelator["Date"] != "")
                optionalDate = DateTime.Parse(dataSet.OptionalCorrelator["Date"]);

            if (vendorName == null || optionalDate == null) return null;
            
            var vendorId = Program.CurrentVendors.Find(x => x.DisplayName == vendorName).EntityId;
            
            return Program.CurrentBills.Find(x => x.CreatedDate == optionalDate && x.VendorId == vendorId && x.TotalBalance == amount);
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
            var separatorColumnValue = !(row["Seperator"] is DBNull) ? row["Seperator"].ToString() : null;

            //This basically says if the row starts with Total return false, if the seperator row has nothing there, but there is
            //an open balance (ie the last line of the report) then go ahead and return false as well.
            //Otherwise we are returning true that the row is valid.
            return  (separatorColumnValue == null || !separatorColumnValue.StartsWith("Total")) &&
                    (separatorColumnValue != null || row["Open Balance"] is DBNull) ||
                    row["Type"].ToString() == "Bill";
        }

        public SaveQuickBooksOnlineBillRequest AddMappings(
            MultiFileImportDataSet billItem, SaveQuickBooksOnlineBillRequest baseRequest)
        {
            baseRequest.BillNumber = billItem.ParentRow["Num"].ToString();
            baseRequest.CreatedDate = DateTime.Parse(billItem.ParentRow["Date"].ToString());
            baseRequest.DueDate = DateTime.Parse(billItem.ParentRow["Due Date"].ToString());
            baseRequest.LineItems = MapLineItems(billItem);
            baseRequest.VendorId =
                Program.CurrentVendors.Find(x => x.DisplayName == billItem.OptionalCorrelator["Vendor"]).EntityId;
            return baseRequest;
        }

        public List<DataRow> GetDetailRows(DataRow parentRow, DataRowCollection detailSet,
            MultiFileImportDataSet parentItem)
        {
            var billVendorName = parentItem.OptionalCorrelator["Vendor"];
            var billAmount = parentItem.OptionalCorrelator["Amount"];
            DateTime? billDate;
            if(parentItem.OptionalCorrelator.ContainsKey("Date") && parentItem.OptionalCorrelator["Date"].ToString() != "")
                billDate = DateTime.Parse(parentItem.OptionalCorrelator["Date"]);
            else
            {
                billDate = null;
            }

            var billNumber = parentRow["Num"].ToString();
            parentItem.DetailRows = new List<DataRow>();
            var foundByNumber = false;
            for (var y = 0; y < detailSet.Count; y++)
            {
                if (detailSet[y]["Type"].ToString() != "Bill")
                    continue;
                
                if (detailSet[y]["Num"] is DBNull || detailSet[y]["Num"].ToString() != billNumber) continue;
                
                foundByNumber = true;
                //Found the right line now go to the next line and add that as the detail or details(for a SPLIT)
                y++;

                while (detailSet[y]["Num"].ToString() == billNumber)
                {
                    parentItem.DetailRows.Add(detailSet[y]);
                    y++;
                }
                return parentItem.DetailRows;
            }
            if (!foundByNumber)
            {
                for (var y = 0; y < detailSet.Count; y++)
                {
                    if (billDate == null ||
                        billVendorName == null ||
                        detailSet[y]["Name"] is DBNull ||
                        detailSet[y]["Date"] is DBNull ||
                        detailSet[y]["Name"].ToString() != billVendorName ||
                        billDate != DateTime.Parse(detailSet[y]["Date"].ToString()))
                    {
                        continue;
                    }

                    //If we found match by name and date we check the credit line of that bill (the total amount)
                    if (detailSet[y]["Credit"].ToString() != billAmount)
                    {
                        continue;
                    }

                    //Move down a line because the next line is the start of the details (or details in the case of SPLIT)
                    y++;

                    //At this point we will
                    while (DateTime.Parse(detailSet[y]["Date"].ToString()) == billDate.Value &&
                           detailSet[y]["Name"].ToString() == billVendorName &&
                           detailSet[y]["Account"].ToString() != "Accounts Payable")
                    {
                        parentItem.DetailRows.Add(detailSet[y]);
                        y++;
                    }
                    return parentItem.DetailRows;
                }
            }
            if (parentItem.DetailRows.Count == 0)
            {
                Logger.Instance.Log("Could not find correlating detail rows for Bill with \r\n "
                                        + "Number: " + parentItem.ParentRow["Num"] + "\r\n "
                                        + "Vendor Name: " + (parentItem.OptionalCorrelator.ContainsKey("Vendor") ? parentItem.OptionalCorrelator["Vendor"] : "NONE") + "\r\n "
                                        + "Date: " + (parentItem.OptionalCorrelator.ContainsKey("Date") ? parentItem.OptionalCorrelator["Date"] : "NONE") + "\r\n "
                                        + "Amount: " + (parentItem.OptionalCorrelator.ContainsKey("Amount") ? parentItem.OptionalCorrelator["Amount"] : "NONE"));
            }
            return parentItem.DetailRows;
        }
    }
}

