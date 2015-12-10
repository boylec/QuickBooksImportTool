using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DLI.Core.Common.Enums.QuickBooksOnline;
using DLI.Core.Common.Requests.QuickBooksOnline;
using DLI.Core.Common.Responses.QuickBooksOnline;
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
            var employeeName = row["Employee"].ToString();
            var spaceIndex = employeeName.IndexOf(" ", StringComparison.Ordinal);
            var employeeFirstName = employeeName.Substring(0, spaceIndex).Trim();
            var employeeLastName = employeeName.Substring(spaceIndex).Trim();

            employeeRequest.FirstName = employeeFirstName;
            employeeRequest.LastName = employeeLastName;
            employeeRequest.Email = row["Main Email"].ToString();
            employeeRequest.Address = row["Address"].ToString();
            employeeRequest.City = row["City"].ToString();
            employeeRequest.DateOfBirth = DateTime.Parse(row["Date of Birth"].ToString());
            employeeRequest.MainPhone = Utils.FormatPhone(row["Main Phone"].ToString());
            employeeRequest.SSNumber = row["SS No."].ToString();
            employeeRequest.State = row["State"].ToString();
            employeeRequest.Zip = row["Zip"].ToString();
            return employeeRequest;
        }

        public QuickBooksOnlineEmployeeResponse GetExistingFromRow(DataRow row)
        {
            var employeeName = row["Employee"].ToString();
            var spaceIndex = employeeName.IndexOf(" ", StringComparison.Ordinal);
            var employeeFirstName = employeeName.Substring(0, spaceIndex).Trim();
            var employeeLastName = employeeName.Substring(spaceIndex).Trim();

            return Program.CurrentEmployees.Find(x => x.FirstName == employeeFirstName && x.LastName == employeeLastName);
        }
    }
}
