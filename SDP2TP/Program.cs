using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;
using MySql.Data.MySqlClient;
using System.Net.Http;
using System.Net;
using System.Xml;
using System.Xml.Serialization;
using System.IO;

namespace SDP2TP
{
    class Program
    {
        // http://www.manageengine.com/products/service-desk/help/adminguide/index.html
        //http://rumsk2hpdm02.east.msk/sdpapi/request/80218?OPERATION_NAME=GET_REQUEST&TECHNICIAN_KEY=FDC4BEF6-6C99-44E3-8217-FBF072DCAAB2

        // globals        
        private static string PATH_SDP = ConfigurationManager.AppSettings["PATH_SDP"];
        private static string PATH_TP = ConfigurationManager.AppSettings["PATH_TP"];
        private static string SDP_API_KEY = "TECHNICIAN_KEY=" + ConfigurationManager.AppSettings["SDP_API_KEY"];
        private static string TP_TOKEN = ConfigurationManager.AppSettings["TP_Token"];

        static void Main(string[] args)
        {

            ProceccSDPRecords(GetSDPRequests());            
        }      

        private static List<SDP.Request> GetSDPRequests()
        {
            List<SDP.Request> rs = new List<SDP.Request>();

            //connect to sdp MySql db to get all needed requests  
            MySqlConnection con = new MySqlConnection(ConfigurationManager.ConnectionStrings["SDP_MySQL"].ConnectionString);
            MySqlCommand cmd = new MySqlCommand();
            //{services}
            string selectServices = ConfigurationManager.AppSettings["SDP_Services_to_select"];
            string selectServiceGoups = ConfigurationManager.AppSettings["SDP_SupportGroups_to_select"];
            //{servicegroups}

            cmd.CommandText = Properties.Resources.SDP_Select_requests.Replace("{services}", selectServices).Replace("{servicegroups}", selectServiceGoups);
                cmd.Connection = con;

            MySqlDataReader reader;
            try
            {
                cmd.Connection.Open();
                reader = cmd.ExecuteReader();                

                while (reader.Read())
                {
                    Console.WriteLine(reader["WORKORDERID"]);
                    SDP.Request r = new SDP.Request();
                    r.WorkorderID = Convert.ToInt32(reader["WORKORDERID"].ToString());
                    r.Title = reader["TITLE"].ToString();
                    r.Technician = reader["Technician"].ToString();
                    r.SupportGroup = reader["SupportGroup"].ToString();
                    r.ServiceName = reader["ServiceName"].ToString();
                    r.Requester_FullName = reader["FullName"].ToString();
                    r.Requester_EMail = reader["Email"].ToString();
                    r.Description = reader["Description"].ToString();
                    r.FullDescription = reader["FullDescription"].ToString();
                    r.ApplicationName = reader["ApplicationName"].ToString();

                    rs.Add(r);
                }
                reader.Close();
            }
            catch (MySqlException ex)
            {
                Console.WriteLine("Error: \r\n{0}", ex.ToString());
            }
            finally
            {
                cmd.Connection.Close();
            }

            return rs;
        }

        private static async Task<HttpResponseMessage> PostRequestToTP(TP.Request r)
        {
            using (var httpClient = new HttpClient())
            {

                HttpRequestMessage request = new HttpRequestMessage();
                request.Method = HttpMethod.Post;
                request.RequestUri = new Uri(Program.PATH_TP + "Requests?" + Program.TP_TOKEN);               
                //request.Content = new StringContent("<Request Name='test request'><Project Id='2584' /><EntityState Id='34' /></Request>", Encoding.UTF8);
                //XmlSerializer x = new XmlSerializer(r.GetType());
                //StringWriter sw = new StringWriter();
                //x.Serialize(sw, r);
                //string s = sw.ToString();
                //request.Content = new StringContent(sw.ToString(), Encoding.UTF8);

                string xmlRequest = "<Request Name='" + r.Name + "'>";
                xmlRequest += "<Project Id='" + r.Project.Id + "' />";
                xmlRequest += "<EntityState Id='34' />";
                //xmlRequest += "<Description>" + WebUtility.HtmlDecode(r.Description) + "</Description>";
                xmlRequest += "<Description><![CDATA[" + r.Description + "]]></Description>";                
                xmlRequest += "</Request>";

                request.Content = new StringContent(xmlRequest, Encoding.UTF8);
                request.Content.Headers.ContentType.MediaType = "application/xml";
                request.Content.Headers.ContentType.CharSet = "utf-8";
                //request.Headers.Add("Content-Type", "application/xml; charset=utf-8");
                
                try
                {
                    HttpResponseMessage response = await httpClient.SendAsync(request);
                    response.EnsureSuccessStatusCode();

                    //return await response.Content.ReadAsStringAsync();
                    return response;
                }
                catch (Exception e)
                {
                    throw e;
                }               
                
            }
        }

        private static void AssignDeveloperToRequest(string requestID, string developerName)
        {
            // check that developer exists in TP
            string developerLastName = developerName.Split(null).ElementAt(0);

            //string getRequest = "Users?where=LastName eq '" + developerName.Split(null).ElementAt(0) + "'&" + TP_TOKEN;
            string getRequest = "Users?where=(LastName eq 'Mironov') and (IsActive eq 'true')&" + TP_TOKEN;
            WebClient wc = new WebClient();
            wc.Encoding = Encoding.UTF8;
            string developerID = "";

            string getResult = wc.DownloadString(PATH_TP + getRequest);
            if (getResult.Length > 8)
            {
                int start = getResult.IndexOf("<User Id=") + 10;
                int end = getResult.IndexOf(">", start + 1) - 1;
                int length = end - start;
                developerID = getResult.Substring(start, length);

                //create assigment
                string postRequest = "<Assignment><Assignable Id='" + requestID + "'/><GeneralUser Id='" + developerID + "'></GeneralUser><Role Id='6'/></Assignment>";
                string postResult = wc.UploadString(PATH_TP + "Assignments?" + TP_TOKEN, "post", postRequest);
            }
        }

        private static void ProceccSDPRecords(List<SDP.Request> sdp_rs)
        {
            //TO-DO: add iterate through sdp_rs
            foreach(SDP.Request r in sdp_rs)
            {
                TP.Project project = new TP.Project(r.ApplicationName); //TO-DO: Add auto assignnent for projects                
                TP.Request req = new TP.Request();
                req.Name = r.Title;
                req.Project = project;               
                req.Description = r.FullDescription.Replace("src=\"/inlineimages/WorkOrder", "src=\"http://rumsk2hpdm01/inlineimages/WorkOrder");

                var task = PostRequestToTP(req);
                task.Wait();

                var response = task.Result;
                var body = response.Content.ReadAsStringAsync().Result;
                
                string requestID = body.Substring(13, body.IndexOf(" Name=") -14);
                
                AssignDeveloperToRequest(requestID, r.Technician);
                
                //Console.WriteLine(body);
            }
                
            //var task = PostRequestToTP(r);
            //task.Wait();

            //var response = task.Result;
            //var body = response.Content.ReadAsStringAsync().Result;
            //Console.WriteLine(body);
        }
    }
}
