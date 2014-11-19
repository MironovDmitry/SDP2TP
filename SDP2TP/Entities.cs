using System.Xml.Serialization;
using System.Net;
using System.Text;
using System.Collections.Generic;
using System;
using System.Configuration;
using Newtonsoft.Json;
using System.Linq;
using System.Web;


namespace SDP2TP
{
    namespace SDP
    {
        public class Request
        {
            public int WorkorderID { get; set; }
            public string Requester_FullName { get; set; }
            public string Requester_EMail { get; set; }
            public string Title { get; set; }
            public string Description { get; set; }
            public string ServiceName { get; set; }
            public string ApplicationName { get; set; }
            public string SupportGroup { get; set; }
            public string Technician { get; set; }
            public string FullDescription { get; set; }
        } 
    }


    namespace TP
    {        
        public class RequestCollection
        {
            public RequestCollection()
            {
                Requests = new Request[0];
            }

            [XmlElement("Request")]
            public Request[] Requests { get; set; }

            [XmlAttribute]
            public string Next { get; set; }
        }
                
        public class Request
        {     
            public int Id { get; set; }
            public string Name { get; set; }
            public string Description { get; set; }
            public string EntityState {
                get 
                {
                    return "New";
                }                
            }
            public Project Project { get; set; }
            public string SDP_Technician { get; set; }
            public string SDP_Requester { get; set; }
            public string SDP_Requester_Email { get; set; }
            public int SDP_ID { get; set; }
            
            public void AddRequestToTP()
            { 
                //post request 
                postRequets(this);
                //Assign developer
                assignDeveloper(this);
                //Assign requester
                assignRequester(this);
            }

            public void updateSDPRequest()
            {
                updateSDPRequestResolution(this);
                updateSDPRequestStatus(this);
            }

            private static bool updateSDPRequestResolution(TP.Request r)
            {
                //http://rumsk2hpdm02.east.msk/sdpapi/request/80218?OPERATION_NAME=GET_REQUEST&TECHNICIAN_KEY=FDC4BEF6-6C99-44E3-8217-FBF072DCAAB2
                ///request/79376/RESOLUTION?OPERATION_NAME=ADD_RESOLUTION&TECHNICIAN_KEY=FDC4BEF6-6C99-44E3-8217-FBF072DCAAB2&INPUT_DATA=<Details><resolution><resolutiontext>test</resolutiontext></resolution></Details>
                
                /*
                <Details>
                    <resolution>
                        <resolutiontext>asd</resolutiontext>
                    </resolution>
                </Details>
                 * */

                string SDP_API_KEY = "TECHNICIAN_KEY=" + ConfigurationManager.AppSettings["SDP_API_KEY"];
                string text = Properties.Resources.SDP_Resolution.Replace("{tp_request_ID}", r.Id.ToString());
                text = text.Replace("{RequesterName}", r.SDP_Requester.Split(null)[1].ToString());
                text = text.Replace("{developerName}", r.SDP_Technician.Split(null)[1].ToString() + " " + r.SDP_Technician.Split(null)[0].ToString());
                text = text.Replace("\r\n", "<br />");
                
                string xmlRequest = "<Details><resolution><resolutiontext>" + HttpUtility.UrlEncode(HttpUtility.HtmlEncode(text)) + "</resolutiontext></resolution></Details>";
                string requestString = "/request/" + r.SDP_ID.ToString() + "/resolution?OPERATION_NAME=ADD_RESOLUTION&" + SDP_API_KEY + "&INPUT_DATA=" + xmlRequest;

                string result = getSDPWebRequestResults(requestString, "POST", "");

                if (result.IndexOf("")>0)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            private static bool updateSDPRequestStatus(TP.Request r)
            {
                /* /request/<request id>
                 * CLOSE_REQUEST
                 * <Operation>
                        <Details>
                            <parameter>
                                <name>closeAccepted</name>
                                <value>Accepted</value>
                            </parameter>
                            <parameter>
                                <name>closeComment</name>
                                <value>The Closing Comment</value>
                            </parameter>
                        </Details>
                    </Operation>
                 * */
                string SDP_API_KEY = "TECHNICIAN_KEY=" + ConfigurationManager.AppSettings["SDP_API_KEY"];
                //string text = Properties.Resources.SDP_Resolution.Replace("{tp_request_ID}", r.Id.ToString());
                string xmlRequest = "<Operation><Details><closeAccepted>Accepted</closeAccepted><closeComment>Закрыто автоматически. ТП request " + r.Id.ToString() + "</closeComment></Details></Operation>";
                string requestString = "/request/" + r.SDP_ID.ToString() + "?OPERATION_NAME=CLOSE_REQUEST&" + SDP_API_KEY + "&INPUT_DATA=" + xmlRequest;

                string result = getSDPWebRequestResults(requestString, "POST", "");

                if (result.IndexOf("") > 0)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            private static bool postRequets(TP.Request r)
            {                
                //build link to SDP
                string sdp_path = ConfigurationManager.AppSettings["PATH_SDP"];
                sdp_path = sdp_path.Substring(0, sdp_path.Length - 7);
                string linkToSDPWO = sdp_path + "/WorkOrder.do?woMode=viewWO&woID=" + r.SDP_ID.ToString() + " <br />";
                string xmlRequest = "<Request Name='" + r.Name + "'>";
                xmlRequest += "<Project Id='" + r.Project.Id + "' />";
                xmlRequest += "<EntityState Id='34' />";                
                xmlRequest += "<Description><![CDATA[" + linkToSDPWO + r.Description + "]]></Description>";
                xmlRequest += "</Request>";

                string result = getWebRequestResults("Requests?", "post", xmlRequest);
                
                //check if successfull
                if (result.IndexOf(" Id=") > 0)
                {
                    r.Id = Convert.ToInt32(result.Substring(13, result.IndexOf(" Name=") - 14));
                    return true;
                }
                else
                {
                    return false;
                }                
            }
            private static bool assignDeveloper(TP.Request r)
            {
                string developerID = r.getDeveloperID();
                if (developerID != "0")
                {
                    string xmlRequest = "<Assignment><Assignable Id='" + r.Id + "'/><GeneralUser Id='" + developerID + "'></GeneralUser><Role Id='6'/></Assignment>";
                    string result = getWebRequestResults("Assignments?", "post", xmlRequest);

                    //check if successfull
                    if (result.IndexOf(" Id=") > 0)
                    {
                        //r.Id = Convert.ToInt32(result.Substring(13, result.IndexOf(" Name=") - 14));
                        
                        return true;
                    }
                    else
                    {
                        return false;
                    }                      
                }
                else
                {
                    return false;
                }
                
            }
            private static bool assignRequester(TP.Request r)
            {
                //first we need to remove service account from requesters
                removeRequesters(r);                                

                //now lets add real requester
                string requesterID = r.getRequesterID();
                string xmlRequest = "<Request Id='" + r.Id.ToString() + "'><Requesters><GeneralUser Id='" + requesterID + "'/></Requesters></Request>";

                string result = getWebRequestResults("Requests?", "post", xmlRequest);

                    //check if successfull
                    if (result.IndexOf(" Id=") > 0)
                    {
                        //update owner
                        ///requests
                        /*
                         * <Request Id="2920">
                            <Owner id="38"/>      
                            </Request>
                         * */

                        xmlRequest = "<Request Id='" + r.Id.ToString() + "'><Owner id='" + requesterID + "'/></Request>";
                        result = getWebRequestResults("Requests?", "POST", xmlRequest);
                                
                        return true;
                    }
                    else
                    {
                        return false;
                    }                
            }
            private string getDeveloperID()
            {
                // check that developer exists in TP
                string developerLastName = SDP_Technician.Split(null).ElementAt(0);

                //string getRequest = "Users?where=LastName eq '" + developerName.Split(null).ElementAt(0) + "'&" + TP_TOKEN;
                string getRequest = "where=(LastName eq 'Mironov') and (IsActive eq 'true')";

                string developerID = "";

                //string getResult = wc.DownloadString(ConfigurationManager.AppSettings["PATH_TP"] + getRequest);
                string getResult = getWebRequestResults("Users?", "get", getRequest);
                if (getResult.Length > 8)
                {
                    int start = getResult.IndexOf("<User Id=") + 10;
                    int end = getResult.IndexOf(">", start + 1) - 1;
                    int length = end - start;
                    developerID = getResult.Substring(start, length);
                }
                else
                {
                    developerID = "0";
                }

                return developerID;

            }
            private string getRequesterID()
            {
                // check that developer exists in TP
                string[] requesterName = SDP_Requester.Split(null);

                //string getRequest = "Users?where=LastName eq '" + developerName.Split(null).ElementAt(0) + "'&" + TP_TOKEN;
                string getRequest = "where=(Email eq '" + SDP_Requester_Email + "')";

                string requesterID = "";

                //string getResult = wc.DownloadString(ConfigurationManager.AppSettings["PATH_TP"] + getRequest);
                string getResult = getWebRequestResults("Requesters?", "get", getRequest);

                if (getResult.IndexOf(" Id=") > 0) //requster exists so get his ID
                //if (getResult.Length > 8)
                {
                    int start = getResult.IndexOf(" Id=") + 5;
                    int end = getResult.IndexOf(">", start + 1) - 1;
                    int length = end - start;
                    requesterID = getResult.Substring(start, length);
                }
                else //requester does not exists so create it first
                {
                    string postRequest = "<Requester><Kind>Requester</Kind><FirstName>" + requesterName[1] + "</FirstName><LastName>" + requesterName[0] + "</LastName><Email>" + SDP_Requester_Email + "</Email></Requester>";
                    getResult = getWebRequestResults("Requesters?", "post", postRequest);

                    int start = getResult.IndexOf(" Id=") + 5;
                    int end = getResult.IndexOf(">", start + 1) - 1;
                    int length = end - start;
                    requesterID = getResult.Substring(start, length);
                }

                return requesterID;
            }
            private static bool removeRequesters(TP.Request r)
            {
                //get ID service account as requester
                string xmlRemoveRequest = "include=[requesters]";
                string result = getWebRequestResults("requests/" + r.Id.ToString() + "?", "GET", xmlRemoveRequest);
                /*
                 * <Request Id="2879">
  <Requesters>
    <GeneralUser Id="15">
      <FirstName>Dmitry</FirstName>
      <LastName>Mironov</LastName>
    </GeneralUser>
  </Requesters>
</Request>
                 * */
                int start = result.IndexOf("User Id=") + 9;
                int end = result.IndexOf(">", start + 1) - 1;
                int len = end - start;
                string requerterID = result.Substring(start, len);
                
                //DELETE на адрес вида /api/v1/requests/9/requesters/16 Где 9 - Request ID, 16 - Requester ID
                string requestString = "requests/" + r.Id.ToString() + "/requesters/" + requerterID + "?";

                result = getWebRequestResults(requestString, "DELETE", "");

                
                return false;
            }
            private static string getWebRequestResults(string requestString, string requestMethod, string data = null)
            {
                WebClient wc = new WebClient();
                wc.Encoding = Encoding.UTF8;
                wc.Headers["Content-Type"] = "application/xml; charset=UTF-8";
                                
                string tp = ConfigurationManager.AppSettings["PATH_TP"] + requestString + ConfigurationManager.AppSettings["TP_Token"];
                if (requestMethod.ToUpper() == "GET")
                {
                    return wc.DownloadString(tp + "&" + data);
                }
                else if (requestMethod.ToUpper() == "POST")
                {
                    return wc.UploadString(tp, "POST", data);                    
                }
                else if (requestMethod.ToUpper() == "DELETE")
                {
                    return wc.UploadString(tp, "DELETE", data); 
                }
                else
                {
                    return "";
                }
            }

            private static string getSDPWebRequestResults(string requestString, string requestMethod, string data = null)
            {
                //string SDP_API_KEY = "TECHNICIAN_KEY=" + ConfigurationManager.AppSettings["SDP_API_KEY"];
                string PATH_SDP = ConfigurationManager.AppSettings["PATH_SDP"];

                WebClient wc = new WebClient();
                wc.Encoding = Encoding.UTF8;
                wc.Headers["Content-Type"] = "application/xml; charset=UTF-8";
                                
                //string tp = ConfigurationManager.AppSettings["PATH_TP"] + requestString + ConfigurationManager.AppSettings["TP_Token"];
                string sdp = PATH_SDP + requestString;
                
                return wc.UploadString(sdp, requestMethod, data);                    
                
            }
        }

      

        
        public class Project
        {
            [XmlAttribute]
            public int Id { get; set; }

            [XmlAttribute]
            public string Name { get; set; }

            public override string ToString()
            {
                return string.Format("Id: {0}, Name: {1}", Id, Name);
            }

            public Project(int projectID)
            {
                this.Id = projectID;              
            }
            public Project(string sdpApplication)
            {                
              //this.Id = GetProjectsIDbyName(sdpApplication);                    
                switch (sdpApplication)
                {
                    case "1CAccounting":
                        this.Id = 16286;
                        break;
                    case "1CBitFinance":
                        this.Id = 16287;
                        break;
                    case "1CConsolidation":
                        this.Id = 16909;
                        break;
                    case "1CDocFlow":
                        this.Id = 18349;
                        break;
                    case "1CHR":
                        this.Id = 16285;
                        break;
                    case "1CMediaController":
                        this.Id = 16284;
                        break;
                    case "1CMediaFinance":
                        this.Id = 3283;
                        break;
                    default:
                        this.Id = 2584;
                        break;
                }

                if(ConfigurationManager.AppSettings["PATH_TP"] == "https://cis.tpondemand.com/api/v1/")
                {
                    this.Id = 2584;
                }
            }

            //private int GetProjectsIDbyName(string sdpApplication)
            //{
            //    //projects?where=Name%20eq%20"test2"
            //    string tp_project_name = "";
            //    switch (sdpApplication)
            //    {
            //        case "1CAccounting":
            //            tp_project_name = "1С: Бухгалтерия";
            //            break;
            //        case "1CBitFinance":
            //            tp_project_name = "1С: Бит-Финанс";
            //            break;
            //        case "1CConsolidation":
            //            tp_project_name = "1С: Бит-Финанс";
            //            break;
            //    }
                
            //    //create web client to get response from webserver
            //    var client = new WebClient();
            //    //client.UseDefaultCredentials = false;            
            //    client.Encoding = Encoding.UTF8;

            //    string PathToTp = ConfigurationManager.AppSettings["PATH_TP"];
            //    Project project = JsonConvert.DeserializeObject<Project>(client.DownloadString(PathToTp + "projects?include=[name,id]&where=(IsActive eq 'true')&take=1000&format=json" + ConfigurationManager.AppSettings["TP_Token"]));

            //    return project.Id;
            //}
        }

        public class GeneralUser
        {
            public string Kind { get; set; }
            public int Id { get; set; }
            public string FirstName { get; set; }
            public string LastName { get; set; }
            public override String ToString()
            {
                return FirstName + " " + LastName;
            }
        }

        public class Requester
        {            
            public int Id { get; set; }
            public string FirstName { get; set; }
            public string LastName { get; set; }
            public override String ToString()
            {
                return FirstName + " " + LastName;
            }
            public string Email { get; set; }
            public string Kind { get; set; }
        }
        
        public class Owner
        {
            public int Id { get; set; }
            public string FirstName { get; set; }
            public string LastName { get; set; }
            public override String ToString()
            {
                return FirstName + " " + LastName;
            }
        }

    }
}
