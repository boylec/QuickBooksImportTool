using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using DLI.Core.Common.DTOs.QuickBooksOnline;
using DLI.Core.Common.Enums.QuickBooksOnline;
using DLI.Core.Common.Requests.QuickBooksOnline;
using DLI.Core.Common.Responses.QuickBooksOnline;
using QboImporterTool.Classes.Bases;
using QboImporterTool.Mapper;

namespace QboImporterTool.Classes
{
    internal class InvoiceImportPackage :
        SplitLinesImportPackage<SaveQuickBooksOnlineInvoiceRequest, QuickBooksOnlineInvoiceResponse>
    {
        public InvoiceImportPackage(string invoiceFile) : base(invoiceFile, new InvoiceMapper(), ImportableTypes.Invoice)
        {
        }
    }



    internal class InvoiceMapper :
        IParentChildMapAdder<SaveQuickBooksOnlineInvoiceRequest, QuickBooksOnlineInvoiceResponse>
    {
        public List<QbOnlineInvoiceLineItem> MapLineItems(List<DataRow> invoiceDetailRows)
        {
            var convertedList = new List<QbOnlineInvoiceLineItem>();
            var discountLines = new List<QbOnlineInvoiceLineItem>();

            //Go through each parentRow and parse some stuff
            foreach (var row in invoiceDetailRows)
            {
                //Goingt o use the item name in order to get a reference to the item from within QBO.
                var fullyQualifiedItemName = GetFullyQualifiedItemName(row["Item"].ToString());

                //Get the reference to the item
                var productOrService =
                    Program.CurrentItems.Find(x => x.FullyQualifiedName == fullyQualifiedItemName);

                //Will be adding each convertedItem to the convertedList which will be returned. (The list of line items for an invoice)
                var convertedItem = new QbOnlineInvoiceLineItem
                {
                    IsSubTotalLine = false,
                    ItemId = productOrService.EntityId,
                    LineDescription = productOrService.Description
                };

                //Need to know if this is a discount item because if it we do the pricing differently. (Qty would be the total number of dollars of the invoice lines
                //that are not discounts. So we do the discount lines after we do the non discount lines. Because we need the non discount lines sum to 
                //know how many dollars will be discounted
                var isDiscount = productOrService.Description != null && productOrService.Description.StartsWith("Discount", StringComparison.CurrentCultureIgnoreCase);
                var isService = !isDiscount && productOrService.IncomeAccountName == "Services";

                if (isDiscount)
                {
                    //Add this line to process after this loop based on the lines that are processed in this next branch.
                    discountLines.Add(convertedItem);
                }
                else if (isService)
                {
                    //If it is a service the amount should be in the Credit column.
                    convertedItem.Amount = Convert.ToDecimal(row["Credit"].ToString());

                    //Add the converted item to the convertedList
                    convertedList.Add(convertedItem);
                }
                else
                {
                    throw new Exception("Cannot determine if invoice line is Discount or Service");
                }
            }

            //At this point all non discount line items for the invoice should be in convertedList, then we figure out the discount line items based on those
            foreach (var discountLine in discountLines)
            {
                //Total number of dollars to be discounted is the sum of all the nondiscount lines
                var numberOfDollarsToBeDiscounted = convertedList.Sum(item => item.Amount);

                //Enter this as the quanity for the discounted line. The rate for that line will be automatically populated based on the itemId we put in.
                //for example the Item 4:DBM might discount -0.8%. So it will calculate 0.8% of all the service line items, and that will be entered 
                //automatically into the amount field of this discount line. (Theoretically)
                discountLine.Qty = numberOfDollarsToBeDiscounted;
            }

            //Now that the discount lines have been mapped/parsed they can be appended to the end
            convertedList.AddRange(discountLines);

            //Return all of the lines for this particular invoice.
            return convertedList;
        }

        private static string GetFullyQualifiedItemName(string itemString)
        {
            var indexOfParenth = itemString.IndexOf('(');
            return indexOfParenth != -1 ? itemString.Substring(0, indexOfParenth).Trim() : itemString.Trim();
        }

        public QuickBooksOnlineInvoiceResponse CheckEntityExistsInQbo(MultiFileImportDataSet dataSet)
        {
            var invoiceDocNumber = dataSet.ParentRow["Num"].ToString();
            return Program.CurrentInvoices.Find(x => x.DocNumber == invoiceDocNumber.ToString());
        }

        public bool IsParentRowValid(DataRow row)
        {
            return (!(row["Type"] is DBNull) && row["Type"].ToString() == "Invoice") && row["Account"].ToString() == "Accounts Receivable";
        }

        public SaveQuickBooksOnlineInvoiceRequest AddMappings(
            MultiFileImportDataSet invoiceItem, SaveQuickBooksOnlineInvoiceRequest baseRequest)
        {
            var customerName = invoiceItem.ParentRow["Name"].ToString();
            baseRequest.CustomerId =
                Program.CurrentCustomers.Find(
                    x => x.DisplayName.Equals(customerName, StringComparison.CurrentCultureIgnoreCase)).EntityId;
            baseRequest.InvoiceNumber = invoiceItem.ParentRow["Num"].ToString();
            baseRequest.SalesTermId = Program.CurrentTerms.Find(x => x.TermName == invoiceItem.ParentRow["Terms"].ToString()).EntityId;
            baseRequest.LineItems = MapLineItems(invoiceItem.DetailRows);
            baseRequest.CreatedDate = DateTime.Parse(invoiceItem.ParentRow["Date"].ToString());
            baseRequest.DueDate = DateTime.Parse(invoiceItem.ParentRow["Due Date"].ToString());
            baseRequest.PurchaseOrderNumber = invoiceItem.ParentRow["P. O. #"].ToString();
            baseRequest.CustomFieldIdForPurchaseOrder =
                Program.CurrentPreferences[0].SalesFormCustomFields.Find(
                    (x => x.ValueType == CustomFieldValueType.StringType && (string) x.Value == "PO Number")).Id;
            return baseRequest;
        }

        public List<DataRow> GetDetailRows(DataRow parentRow, DataRowCollection detailSet,
            MultiFileImportDataSet parentItem)
        {
            var invoiceNumber = parentRow["Num"].ToString();
            parentItem.DetailRows = new List<DataRow>();
            for (var y = 0; y < detailSet.Count; y++)
            {
                if (detailSet[y]["Type"].ToString() != "Invoice" ||
                    detailSet[y]["Num"].ToString() != invoiceNumber) continue;

                var thisInvoiceNumber = detailSet[y]["Num"].ToString();

                y++;

                while (detailSet[y]["Num"].ToString() == thisInvoiceNumber)
                {
                    parentItem.DetailRows.Add(detailSet[y]);
                    y++;
                }
            }
            if (parentItem.DetailRows.Count == 0)
            {
                Logger.Instance.Log("Could not find correlating detail rows for Invoice with \r\n "
                                    + "Number: " + invoiceNumber + "\r\n ");
            }
            return parentItem.DetailRows;
        }

        /// <summary>
        /// For an invoice the detail row will basically always be a different account than the account
        /// on the parent row. (The account on the parent row should always be Accounts Receivable)
        /// </summary> 
        public bool IsDetailRowValid(DataRow detailRow, DataRow parentRowForDetail)
        {
            return detailRow["Account"].ToString() != parentRowForDetail["Account"].ToString() && !(detailRow["Type"] is DBNull) && detailRow["Type"].ToString() == "Invoice";
        }
    }
}

