using System.Data;
using DLI.Core.Common.Requests.QuickBooksOnline;
using DLI.Core.Common.Responses.QuickBooksOnline;
using QboImporterTool.Classes.Bases;
using QboImporterTool.Mapper;

namespace QboImporterTool.Classes
{
    internal class TermImportPackage :
        BaseImportPackage<SaveQuickBooksOnlineTermRequest, QuickBooksOnlineTermResponse>
    {
        public TermImportPackage(string fileName) : base(fileName, new TermMapper(), ImportableTypes.Term)
        {

        }
    }

    internal class TermMapper : IRowToRequestMapper<SaveQuickBooksOnlineTermRequest, QuickBooksOnlineTermResponse>
    {

        public SaveQuickBooksOnlineTermRequest AddMappingsToBaseRequest(DataRow row,
            SaveQuickBooksOnlineTermRequest request)
        {
            request.DayOfMonthDue = int.Parse(row["Day Of Month Due"].ToString());
            request.DiscountDays = int.Parse(row["Discount days"].ToString());
            request.DiscountOnDayOfMonth = int.Parse(row["Discount on Day of Month"].ToString());
            request.NetDueDays = int.Parse(row["Net due days"].ToString());
            request.DiscountPercent = decimal.Parse(row["Discount %"].ToString());
            request.TermName = row["Term"].ToString();
            request.Type = row["Type"].ToString();
            return request;
        }

        public QuickBooksOnlineTermResponse GetExistingFromRow(DataRow row)
        {
            var termName = row["Term"].ToString();
            return Program.CurrentTerms.Find(x => x.TermName == termName.ToString());
        }
    }
}
