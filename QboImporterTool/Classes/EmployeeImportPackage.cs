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
    internal class EmployeeImportPackage :
        BaseImportPackage<SaveQuickBooksOnlineEmployeeRequest, QuickBooksOnlineEmployeeResponse>
    {
        public EmployeeImportPackage(string fileName)
            : base(fileName, new EmployeeMapper(), ImportableTypes.Employee)
        {

        }
    }

    internal class EmployeeMapper : IRowToRequestMapper<SaveQuickBooksOnlineEmployeeRequest, QuickBooksOnlineEmployeeResponse>
    {
        public SaveQuickBooksOnlineEmployeeRequest AddMappingsToBaseRequest(DataRow row, SaveQuickBooksOnlineEmployeeRequest employeeRequest)
        {
            employeeRequest.FirstName = row["First Name"].ToString();
            employeeRequest.LastName = row["Last Name"].ToString();
            employeeRequest.MiddleInitial = row["M.I."].ToString();
            employeeRequest.Gender = row["Gender"].ToString();
            employeeRequest.Street1 = row["Street1"].ToString();
            employeeRequest.Street2 = row["Street2"].ToString();
            employeeRequest.City = row["City"].ToString();
            employeeRequest.DateOfBirth = DateTime.Parse(row["Date of Birth"].ToString());
            employeeRequest.MainPhone = Utils.FormatPhone(row["Main Phone"].ToString().Replace(".", "-").Replace("/", "-"));
            employeeRequest.SSNumber = row["SS No."].ToString();
            employeeRequest.State = row["State"].ToString();
            employeeRequest.Zip = row["Zip"].ToString();
            employeeRequest.HireDate = Convert.ToDateTime(row["Hire Date"].ToString());
            return employeeRequest;
        }

        public QuickBooksOnlineEmployeeResponse GetExistingFromRow(DataRow row)
        {
            var employeeFirstName = row["First Name"].ToString();
            var employeeLastName = row["Last Name"].ToString();
            var employeeMiddleName = !(row["M.I."] is DBNull) ? row["M.I."].ToString() : null;
            return Program.CurrentEmployees.Find(x => x.FirstName == employeeFirstName && x.LastName == employeeLastName && x.MiddleInitial == employeeMiddleName);
        }
    }
}
