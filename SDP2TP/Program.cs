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
using NLog;
using System.Web;

namespace SDP2TP
{
    class Program
    {        
        private static Logger logger = LogManager.GetCurrentClassLogger();
        static void Main(string[] args)
        {
            logger.Info("************************ START ******************************************");
            logger.Info("");
            logger.Info("SDP2TP started");
            ProceccSDPRecords(GetSDPRequests());
            
            logger.Info("");
            logger.Info("************************ END ******************************************");
        }
                
        private static List<SDP.Request> GetSDPRequests()
        {
            logger.Info("Start collecting SDP requests");
            List<SDP.Request> rs = new List<SDP.Request>();

            //connect to sdp MySql db to get all needed requests  
            MySqlConnection con = new MySqlConnection(ConfigurationManager.ConnectionStrings["SDP_MySQL"].ConnectionString);
            MySqlConnection con2 = new MySqlConnection(ConfigurationManager.ConnectionStrings["SDP_MySQL"].ConnectionString);
            logger.Info("Connecting to : " + con.ConnectionString);
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
                logger.Trace("Openning MySQL connection");
                cmd.Connection.Open();
                reader = cmd.ExecuteReader();

                logger.Trace("reader hasRows = " + reader.HasRows);

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
                    r.tpEntityType = reader["tpEntityType"].ToString();
                    r.ccRecepients = reader["CC_Recepients"].ToString();
                    r.Messages = getSDPMessages(con2, r.WorkorderID);

                    logger.Trace("WorkorderID : " + r.WorkorderID);

                    rs.Add(r);
                }
                reader.Close();
            }
            catch (MySqlException ex)
            {
                logger.Trace(ex.ToString());
                Console.WriteLine("Error: \r\n{0}", ex.ToString());
            }
            finally
            {
                cmd.Connection.Close();
                logger.Trace("MySQL connection closed");
            }

            logger.Trace("Total number of SDP.Requests to process = " + rs.Count.ToString());
            return rs;
        }
             
        private static List<SDP2TP.Message> getSDPMessages(MySqlConnection con, int workorderID)
        {
            //SDP.MessageCollection messages = new SDP.MessageCollection();
            List<SDP2TP.Message> messages = new List<SDP2TP.Message>();
            
            MySqlCommand cmd = new MySqlCommand();
            cmd.CommandText = Properties.Resources.SDP_select_Messages_by_workorderID_sql.Replace("{WorkorderID}", workorderID.ToString());
            cmd.Connection = con;

            MySqlDataReader reader;
            try
            {
                if (con.State != System.Data.ConnectionState.Open)
                { 
                    cmd.Connection.Open();
                }
                
                reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    SDP2TP.Message m = new SDP2TP.Message();
                    m.ID = Convert.ToInt32(reader["ID"].ToString());
                    m.MessageDate = Convert.ToDateTime(reader["MessageDate"].ToString());
                    m.SenderID = Convert.ToInt32(reader["SenderID"].ToString());
                    m.SenderFullName = reader["SenderFullName"].ToString();
                    m.SenderEmail = reader["SenderEmail"].ToString();
                    m.Description = reader["Description"].ToString();

                    messages.Add(m);
                }
                reader.Close();
            }
            catch (MySqlException ex)
            {
                Console.WriteLine("Error: \r\n{0}", ex.ToString());
            }

            return messages;
        }

        private static void ProceccSDPRecords(List<SDP.Request> sdp_rs)
        {
            logger.Trace("Start processing SDP.Requests");
            foreach(SDP.Request r in sdp_rs)
            {
                logger.Trace("Processing SDP.Request : " + r.WorkorderID);
                TP.Project project = new TP.Project(r.ApplicationName); //TO-DO: Add auto assignnent for projects                
                TP.Entity req = new TP.Entity();
                req.Name = r.Title;
                req.Project = project;               
                req.Description = r.FullDescription.Replace("src=\"/inlineimages/WorkOrder", "src=\"http://rumsk2hpdm01/inlineimages/WorkOrder");
                req.SDP_Technician = r.Technician;
                req.SDP_Requester = r.Requester_FullName;
                req.SDP_Requester_Email = r.Requester_EMail;
                req.SDP_ID = r.WorkorderID;
                req.EntityType = r.tpEntityType;
                req.CC_Recepients = r.ccRecepients;
                req.Messages = r.Messages;

                logger.Trace("Add request to TP");
                req.AddRequestToTP();
                req.updateSDPRequest();                                
            }            
        }        
    }
}
