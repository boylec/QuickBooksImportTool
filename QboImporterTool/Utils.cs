using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DLI.Core.Common.DTOs.QuickBooksOnline;
using Excel;
using Newtonsoft.Json;
using RestSharp;

namespace QboImporterTool
{
    public static class Utils
    {
        internal static void ParseCityStateZip(string locationString, out string city, out string state, out string zip)
        {
            city = "";
            state = "";
            zip = "";

            var startZipIndex = 0;
            if (locationString.Substring(locationString.Length - 4, 4).All(char.IsDigit))
            {
                if (locationString.Substring(locationString.Length - 5, 1) == "-")
                {
                    if (locationString.Substring(locationString.Length - 10, 5).All(char.IsDigit))
                    {
                        zip = locationString.Substring(locationString.Length - 10, 10);
                        startZipIndex = locationString.Length - 10;
                    }
                    else
                    {
                        throw new Exception("Zip parse error");
                    }
                }
                else if (locationString.Substring(locationString.Length - 5, 5).All(char.IsDigit))
                {
                    zip = locationString.Substring(locationString.Length - 5, 5);
                    startZipIndex = locationString.Length - 5;
                }
            }
            else if (locationString.Substring(locationString.Length - 3, 3).All(char.IsDigit) && locationString.Substring(locationString.Length - 4, 1) == " ")
            {
                zip = locationString.Substring(locationString.Length - 3, 3);
                startZipIndex = locationString.Length - 3;
            }
            else
            {
                throw new Exception("Zip parse error");
            }

            //Zip parsing done.
            var commaIndex = locationString.IndexOf(",", StringComparison.Ordinal);
            city = locationString.Substring(0, commaIndex).Trim(' ');
            state = locationString.Substring(commaIndex + 1, startZipIndex - (commaIndex + 1)).Trim(' ');
        }

        internal static int GetPrevColonFromIndexInString(int startIndex, string stringToCheck)
        {
            var charArray = stringToCheck.ToCharArray();
            for (var x = startIndex - 1; x > 0; x--)
            {
                if (charArray[x] == ':')
                {
                    return x;
                }
            }
            return -1;

        }

        internal static QbOnlinePhoneNumber FormatPhone(string phoneString)
        {
            var phoneNumber = new QbOnlinePhoneNumber();
            var beginExtensionIndex = phoneString.IndexOf("e", StringComparison.OrdinalIgnoreCase);
            if (beginExtensionIndex <= 0)
                beginExtensionIndex = phoneString.IndexOf("x", StringComparison.OrdinalIgnoreCase);
            if (beginExtensionIndex == -1)
            {
                phoneNumber.PhoneNumber = phoneString.Replace("/", "-").Replace(".", "-").Trim();
                phoneNumber.Extension = "";
            }
            else
            {
                phoneNumber.PhoneNumber =
                    phoneString.Substring(0, beginExtensionIndex).Replace("/", "-").Replace(".", "-").Trim();
                var restOfString = phoneString.Substring(beginExtensionIndex + 1);
                phoneNumber.Extension = new string(restOfString.Where(char.IsDigit).ToArray()).Trim();
            }
            return phoneNumber;
        }

        internal static DataRowCollection GetRowsFromExcelFile(string filePath)
        {
            Console.WriteLine(@"Opening " + filePath);
            var stream = File.Open(filePath, FileMode.Open, FileAccess.Read);

            //There are different readers depending on the excel file type...
            var fileEndsInXls = filePath.EndsWith(".xls");
            var fileEndsInXlsx = filePath.EndsWith(".xlsx");

            IExcelDataReader excelReader;

            //Get the excel reader or throw an exception if the file type is wrong
            if (fileEndsInXls)
                excelReader = ExcelReaderFactory.CreateBinaryReader(stream);
            else if (fileEndsInXlsx)
                excelReader = ExcelReaderFactory.CreateOpenXmlReader(stream);
            else
                throw new Exception("All import files must be either .xls or .xlsx format");

            //Get the rows from the reader, each row represents an individual entity (ie a single customer or a single vendor)
            excelReader.IsFirstRowAsColumnNames = true;

            Console.WriteLine(@"Extracting dataset from " + filePath + @"...");
            var dataSet = excelReader.AsDataSet();
            excelReader.Close();

            Console.WriteLine(@"Data extracted...");
            return dataSet.Tables[0].Rows;
        }

        internal static List<TResponseType> GetAllEntities<TResponseType>(string entityName)
        {
            Console.WriteLine("Getting all {0}s from QBO to determine if import request should be create/update", entityName);
            var restClient = new RestClient(Program.QboIntegrationDomain) { Timeout = 0 };
            var request = new RestRequest("api/QuickBooks/" + entityName + "/GetAll", Method.GET);
            request.AddHeader("authToken", "db55d34e-eacd-42cb-b1ba-48f724b35103");
            var response = restClient.Execute(request);
            if (response.ResponseStatus != ResponseStatus.Completed)
            {
                throw new Exception("Error importing entities of type " + entityName + "...");
            }
            
            var deserialized = JsonConvert.DeserializeObject<List<TResponseType>>(response.Content);
            return deserialized;
        }
    }
}