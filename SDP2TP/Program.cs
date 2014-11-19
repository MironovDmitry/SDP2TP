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

using System.Web;

namespace SDP2TP
{
    class Program
    {
        // http://www.manageengine.com/products/service-desk/help/adminguide/index.html
        //http://rumsk2hpdm02.east.msk/sdpapi/request/80218?OPERATION_NAME=GET_REQUEST&TECHNICIAN_KEY=FDC4BEF6-6C99-44E3-8217-FBF072DCAAB2

        // globals        
        //private static string PATH_SDP = ConfigurationManager.AppSettings["PATH_SDP"];
        //private static string PATH_TP = ConfigurationManager.AppSettings["PATH_TP"];
        //private static string SDP_API_KEY = "TECHNICIAN_KEY=" + ConfigurationManager.AppSettings["SDP_API_KEY"];
        //private static string TP_TOKEN = ConfigurationManager.AppSettings["TP_Token"];

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
             
        private static void ProceccSDPRecords(List<SDP.Request> sdp_rs)
        {            
            foreach(SDP.Request r in sdp_rs)
            {                
                TP.Project project = new TP.Project(r.ApplicationName); //TO-DO: Add auto assignnent for projects                
                TP.Request req = new TP.Request();
                req.Name = r.Title;
                req.Project = project;               
                req.Description = r.FullDescription.Replace("src=\"/inlineimages/WorkOrder", "src=\"http://rumsk2hpdm01/inlineimages/WorkOrder");
                req.SDP_Technician = r.Technician;
                req.SDP_Requester = r.Requester_FullName;
                req.SDP_Requester_Email = r.Requester_EMail;
                req.SDP_ID = r.WorkorderID;

                req.AddRequestToTP();
                req.updateSDPRequest();                                
            }            
        }
    }
}
