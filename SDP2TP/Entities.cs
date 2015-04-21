using System.Xml.Serialization;
using System.Net;
using System.Text;
using System.Collections.Generic;
using System;
using System.Configuration;
//using Newtonsoft.Json;
using System.Linq;
using System.Web;
using NLog;


namespace SDP2TP
{    
    public class Message
    {
        public int ID { get; set; }
        public DateTime MessageDate { get; set; }
        public int SenderID { get; set; }
        public string SenderFullName { get; set; }
        public string SenderEmail { get; set; }
        public string Description { get; set; }
    }

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
            public string tpEntityType { get; set; }
            public string ccRecepients { get; set; }
            //public MessageCollection Messages { get; set; }
            public List<SDP2TP.Message> Messages { get; set; }
        }
    }


    namespace TP
    {        
        //public class RequestCollection
        //{
        //    public RequestCollection()
        //    {
        //        Requests = new Entity[0];
        //    }

        //    [XmlElement("Request")]
        //    public Entity[] Requests { get; set; }

        //    [XmlAttribute]
        //    public string Next { get; set; }
        //}
                
        public class Entity
        {
            //private int _entityStateID = 0;
            private static Logger logger = LogManager.GetCurrentClassLogger();

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
                logger.Trace(this.SDP_ID.ToString());
                //post request 
                logger.Trace("-- Posting to TP");
                postRequet(this);
                //add cc recepients
                logger.Trace("-- Adding CC recepients");
                addCCRecepinets(this);
                //Assign developer
                logger.Trace("-- Assigning developer");
                assignDeveloper(this);
                if(this.Messages.Count > 0)
                {
                    logger.Trace("-- Adding comments");
                    addComments(this);
                }                
                //Assign requester
                if (this.EntityType.ToUpper() == "REQUEST")
                {
                    logger.Trace("-- Assigning requester");
                    assignRequester(this);
                }
                //Assign team
                logger.Trace("-- Assigning team");
                assignTeam(this);
            }

                       

            public void updateSDPRequest()
            {
                logger.Trace("-- Update DSP request");
                updateSDPRequestResolution(this);
                updateSDPRequestStatus(this);
            }
            public string EntityType { get; set; }
            public string CC_Recepients { get; set; }
            public List<SDP2TP.Message> Messages { get; set; }            

            private void addComments(Entity entity)
            {
                logger.Trace("-- Adding Comments = " + entity.Messages.Count);
                for (int i = 0; i < entity.Messages.Count; i++)
                {
                    string xmlRequest = "<Comment>";
                    xmlRequest += "<Description><![CDATA[" + entity.Messages[i].Description.Replace("src=\"/inlineimages/WorkOrder", "src=\"http://rumsk2hpdm01/inlineimages/WorkOrder") + "]]></Description>";
                    xmlRequest += "<Owner Id='" + entity.getRequesterID(entity.Messages[i].SenderEmail, entity.Messages[i].SenderFullName).ToString() + "'/>";
                    xmlRequest += "<General Id='" + entity.Id.ToString() + "'/>";
                    xmlRequest += "</Comment>";

                    string result = getWebRequestResults("Comments?", "POST", xmlRequest);
                }
                logger.Trace("-- Comments added");
            }            
            private static bool updateSDPRequestResolution(TP.Entity r)
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
                logger.Trace("---- Start updating SDP request");
                string SDP_API_KEY = "TECHNICIAN_KEY=" + ConfigurationManager.AppSettings["SDP_API_KEY"];
                string text = Properties.Resources.SDP_Resolution.Replace("{tp_request_ID}", r.Id.ToString());
                text = text.Replace("{RequesterName}", r.SDP_Requester.Split(null)[1].ToString());
                text = text.Replace("{developerName}", r.SDP_Technician.Split(null)[1].ToString() + " " + r.SDP_Technician.Split(null)[0].ToString());
                text = text.Replace("\r\n", "<br />");
                
                string xmlRequest = "<Details><resolution><resolutiontext>" + HttpUtility.UrlEncode(HttpUtility.HtmlEncode(text)) + "</resolutiontext></resolution></Details>";
                string requestString = "/request/" + r.SDP_ID.ToString() + "/resolution?OPERATION_NAME=ADD_RESOLUTION&" + SDP_API_KEY + "&INPUT_DATA=" + xmlRequest;

                logger.Trace("---- Sent to SDP");
                string result = getSDPWebRequestResults(requestString, "POST", "");
                
                if (result.IndexOf("<statuscode>200") > 0)
                {
                    logger.Trace("---- Sent to SDP = OK");
                    logger.Trace("---- Send email to requester");
                    SendEmail(r, text);
                    return true;
                }
                else
                {
                    logger.Trace("---- Sent to SDP = FAILED");
                    return false;
                }
            }
            private static void SendEmail(Entity entity, string text)
            {
                //String subject = "TP " + reportType + " Recap : " + reportStartDate.ToString("dddd dd MMMM", CultureInfo.CreateSpecificCulture("ru-RU")) + (reportType == "Weekly" ? " - " + reportEndDate.ToString("dddd dd MMMM", CultureInfo.CreateSpecificCulture("ru-RU")) : "");
                string subject = "Вашей заявке '" + entity.Name + "' присвоен новый номер #" + entity.Id.ToString();
                string senderAddress = "support@aemedia.ru";
                string recepinetsList = entity.SDP_Requester_Email;
                //String recepinetsList = "Dmitry.mironov@dentsuaegis.ru";
                String serviceName = "SDP2TP";                
                String bodyHTML = text;

                var mailClient = new AMService_SendMail.AMServiceClient();
                mailClient.AddToMailQueueAsIs(subject, bodyHTML, senderAddress, recepinetsList, 5, AMService_SendMail.PriorityEnum.Normal, null, serviceName);
                mailClient.Close();                
            }
            private static bool updateSDPRequestStatus(TP.Entity r)
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
                logger.Trace("---- Start updating SDP request status");
                string SDP_API_KEY = "TECHNICIAN_KEY=" + ConfigurationManager.AppSettings["SDP_API_KEY"];
                //string text = Properties.Resources.SDP_Resolution.Replace("{tp_request_ID}", r.Id.ToString());
                string xmlRequest = "<Operation><Details><closeAccepted>Accepted</closeAccepted><closeComment>Закрыто автоматически. ТП request " + r.Id.ToString() + "</closeComment></Details></Operation>";
                string requestString = "/request/" + r.SDP_ID.ToString() + "?OPERATION_NAME=CLOSE_REQUEST&" + SDP_API_KEY + "&INPUT_DATA=" + xmlRequest;

                logger.Trace("---- Sent to SDP");
                string result = getSDPWebRequestResults(requestString, "POST", "");

                if (result.IndexOf("<statuscode>200") > 0)
                {
                    logger.Trace("---- Status updated = OK");
                    return true;
                }
                else
                {
                    logger.Trace("---- Status updated = FAILED");
                    return false;
                }
            }
            private static bool postRequet(TP.Entity r)
            {                
                //build link to SDP
                string sdp_path = ConfigurationManager.AppSettings["PATH_SDP"];
                sdp_path = sdp_path.Substring(0, sdp_path.Length - 7);
                string linkToSDPWO = sdp_path + "/WorkOrder.do?woMode=viewWO&woID=" + r.SDP_ID.ToString() + " <br /><br />  ";
                
                //depending on tpEntityType in sdpRequest we need to create different tp entities
                string xmlRequest = "";
                string tpAPIEntity = "";
                
                switch (r.EntityType.ToUpper())
                { 
                    case "USERSTORY":
                        xmlRequest = "<UserStory Name='" + r.Name + "'>";
                        xmlRequest += "<Project Id='" + r.Project.Id + "' />";
                        xmlRequest += "<EntityState Id='" + getInitialEntityStateID(r.Project.Id,r.EntityType).ToString() + "' />";                
                        xmlRequest += "<Description><![CDATA[" + linkToSDPWO + r.Description + "]]></Description>";
                        //xmlRequest += "<Owner id='" + r.getRequesterID() + "'/>";
                        //set owner = developer (#3047)
                        xmlRequest += "<Owner id='" + r.getDeveloperID() + "'/>";
                        xmlRequest += "</UserStory>";

                        tpAPIEntity = "UserStories?";
                        break;
                    case "BUG":
                        xmlRequest = "<Bug Name='" + r.Name + "'>";
                        xmlRequest += "<Project Id='" + r.Project.Id + "' />";
                        xmlRequest += "<EntityState Id='" + getInitialEntityStateID(r.Project.Id,r.EntityType).ToString() + "' />";                
                        xmlRequest += "<Description><![CDATA[" + linkToSDPWO + r.Description + "]]></Description>";
                        //xmlRequest += "<Owner id='" + r.getRequesterID() + "'/>";
                        //set owner = developer (#3047)
                        xmlRequest += "<Owner id='" + r.getDeveloperID() + "'/>";
                        xmlRequest += "</Bug>";

                        tpAPIEntity = "Bugs?";
                        break;
                    case "REQUEST":
                        xmlRequest = "<Request Name='" + r.Name + "'>";
                        xmlRequest += "<Project Id='" + r.Project.Id + "' />";
                        xmlRequest += "<EntityState Id='" + getInitialEntityStateID(r.Project.Id,r.EntityType).ToString() + "' />";                
                        xmlRequest += "<Description><![CDATA[" + linkToSDPWO + r.Description + "]]></Description>";
                        xmlRequest += "<Owner id='" + r.getRequesterID() + "'/>";
                        xmlRequest += "</Request>";
                        
                        tpAPIEntity = "Requests?";
                        break;

                }                

                string result = getWebRequestResults(tpAPIEntity, "POST", xmlRequest);
                
                //check if successfull
                string searchString = r.EntityType + " Id=";
                if (result.IndexOf(searchString) > 0)
                {
                    int start = result.IndexOf(searchString) + searchString.Length + 1;
                    int end = result.IndexOf(" Name=") - 1;
                    int len = end - start;

                    r.Id = Convert.ToInt32(result.Substring(start,len));

                    return true;
                }
                else
                {
                    return false;
                }                
            }
            private static bool addCCRecepinets(TP.Entity entity)
            {
                /*
                 * <Request Id="2972">
	                    <CustomFields>		
		                    <Name>ccRecepients</Name>
		                    <Value nil="true"/>		
	                    </CustomFields>
                    </Request>
                 * */
                /*
                 * <Bug Id="2999">
                        <CustomFields>                        
                            <Name>ccRecepients</Name>
                            <Value nil="true"/>
                        </CustomFields>
                    </Bug>
                 */
                string type = entity.EntityType + "s";
                if (entity.EntityType.ToUpper() == "USERSTORY")
                {
                    type = "UserStories";
                    if (entity.CC_Recepients != "")
                    {
                        entity.CC_Recepients += ";"+entity.SDP_Requester_Email;
                    }
                    else
                    {
                        entity.CC_Recepients = entity.SDP_Requester_Email;
                    }                    
                }
                else if (entity.EntityType.ToUpper() == "BUG")
                {
                    if (entity.CC_Recepients != "")
                    {
                        entity.CC_Recepients += ";" + entity.SDP_Requester_Email;
                    }
                    else
                    {
                        entity.CC_Recepients = entity.SDP_Requester_Email;
                    }
                }

                
                string request = "<" + entity.EntityType + " Id='" + entity.Id + "'>";
                request += "<CustomFields>";
                request += "<Field>";
                request += "<Name>ccRecepients</Name>";
                request += "<Value>" + entity.CC_Recepients + "</Value>";
                request += "</Field>";
                request += "</CustomFields>";
                request += "</" + entity.EntityType + ">";
                
                string result = getWebRequestResults(type + "?", "POST", request);
                return true;
            }
            
            private static bool assignDeveloper(TP.Entity r)
            {
                string developerID = r.getDeveloperID();
                if (developerID != "0")
                {
                    /*
                     * <Role Id="6" Name="Support Person">
                     * <Role Id="1" Name="Developer">
                     */
                    string roleID = "1";
                    if (r.EntityType.ToUpper() == "REQUEST")
                    {
                        roleID = "6";
                    }

                    string xmlRequest = "<Assignment><Assignable Id='" + r.Id + "'/><GeneralUser Id='" + developerID + "'></GeneralUser><Role Id='" + roleID + "'/></Assignment>";
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
            private static bool assignRequester(TP.Entity r)
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

                    //xmlRequest = "<Request Id='" + r.Id.ToString() + "'><Owner id='" + requesterID + "'/></Request>";
                        //result = getWebRequestResults("Requests?", "POST", xmlRequest);
                                
                        return true;
                    }
                    else
                    {
                        return false;
                    }                
            }
            private void assignTeam(TP.Entity r)
            {
                //<UserStory id='3219'>
                //  <teamStates>    
                //    <teamState>
                //        <Team id='3220'/>
                //    </teamState>
                //  </teamStates>
                //</UserStory>
                
                string result = "";

                Dictionary<string,Int16> maps = ConfigurationManager.AppSettings["tpProject_Team_Mapping"].Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(map => map.Split(':')).ToDictionary(map => map[0], map => Convert.ToInt16(map[1]));
                
                if (maps.ContainsKey(r.Project.Id.ToString()) && (r.EntityType.ToUpper() == "USERSTORY" | r.EntityType.ToUpper() == "BUG"))
                {
                    //teamID = maps[r.Project.Id.ToString()];
                    string tpAPITntities = "Userstories";

                    switch (r.EntityType.ToUpper())
                    {
                        case "USERSTORY":
                            tpAPITntities = "Userstories?";
                            break;
                        case "BUG":
                            tpAPITntities = "Bugs?";
                            break;
                    }

                    string xmlRequest = "<" + r.EntityType + " id='" + r.Id + "'><teamStates><teamState><Team id='";
                    xmlRequest += maps[r.Project.Id.ToString()].ToString() + "'/></teamState></teamStates></" + r.EntityType + ">";
                    result = getWebRequestResults(tpAPITntities, "post", xmlRequest);
                }                                 
            }
            private string getDeveloperID()
            {
                // check that developer exists in TP
                string developerLastName = SDP_Technician.Split(null).ElementAt(0);

                //string getRequest = "Users?where=LastName eq '" + developerName.Split(null).ElementAt(0) + "'&" + TP_TOKEN;
                //string getRequest = "where=(LastName eq 'Mironov') and (IsActive eq 'true')";
                string getRequest = "where=(LastName eq '" + developerLastName + "') and (IsActive eq 'true')";

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
            private string getRequesterID(string userEmail = "", string userFullName = "")
            {
                string[] requesterName;
                string email;

                // check that developer exists in TP
                if (userFullName == "")
                {
                    requesterName = SDP_Requester.Split(null);
                }
                else
                {
                    requesterName = userFullName.Split(null);
                }

                if (userEmail == "")
                {
                    email = SDP_Requester_Email;                    
                }
                else
                {
                    email = userEmail;
                }

                string getRequest = getRequest = "where=(Email eq '" + email + "')";
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
                    string postRequest = "<Requester><Kind>Requester</Kind><FirstName>" + requesterName[1] + "</FirstName><LastName>" + requesterName[0] + "</LastName><Email>" + email + "</Email></Requester>";
                    getResult = getWebRequestResults("Requesters?", "post", postRequest);

                    int start = getResult.IndexOf(" Id=") + 5;
                    int end = getResult.IndexOf(">", start + 1) - 1;
                    int length = end - start;
                    requesterID = getResult.Substring(start, length);
                }

                return requesterID;
            }
            private static int getInitialEntityStateID(int projectID, string tpEntityType)
            {
                string reqProcess = "projects/" + projectID+"?include=[process[id]]&";
                string result = getWebRequestResults(reqProcess, "GET");
                int start = result.IndexOf("ss Id=") + 7;
                int end = result.IndexOf(" />") - 1;
                int len = end - start;
                string processID = result.Substring(start, len);

                string reqEntityState = "entitystates?where=(process.id eq " + processID + ") and (entitytype.name eq '" + tpEntityType + "') and (IsInitial eq 'true')&include=[id]&";
                result = getWebRequestResults(reqEntityState, "GET");
                 start = result.IndexOf("Id=") + 4;
                 end = result.IndexOf(" />") - 1;
                 len = end - start;
                string EntityStateID = result.Substring(start, len);

                return Convert.ToInt16(EntityStateID);
            }
            private static bool removeRequesters(TP.Entity r)
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
                    try
                    {
                        return wc.DownloadString(tp + "&" + data);
                    }
                    catch
                    {
                              return "";                 
                    }
                }
                else if (requestMethod.ToUpper() == "POST")
                {
                    try
                    {
                        return wc.UploadString(tp, "POST", data);
                    }
                    catch 
                    {
                        return "";
                        
                    }                    
                }
                else if (requestMethod.ToUpper() == "DELETE")
                {
                    try
                    {
                        return wc.UploadString(tp, "DELETE", data);
                    }
                    catch 
                    {
                        return "";                    
                    } 
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
                Dictionary<string,Int16> maps = ConfigurationManager.AppSettings["sdpApplication_TPProject_Mapping"].Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(map => map.Split(':')).ToDictionary(map => map[0], map => Convert.ToInt16(map[1]));

                if (maps.ContainsKey(sdpApplication))
                {
                    this.Id = maps[sdpApplication];
                }
                else
                {
                    this.Id = maps["default"];
                }
                
                
                //switch (sdpApplication)
                //{
                //    case "1CAccounting":
                //        this.Id = 16286;
                //        break;
                //    case "1CBitFinance":
                //        this.Id = 16287;
                //        break;
                //    case "1CConsolidation":
                //        this.Id = 16909;
                //        break;
                //    case "1CDocFlow":
                //        this.Id = 18349;
                //        break;
                //    case "1CHR":
                //        this.Id = 16285;
                //        break;
                //    case "1CMediaController":
                //        this.Id = 16284;
                //        break;
                //    case "1CMediaFinance":
                //        this.Id = 3283;
                //        break;
                //    default:
                //        this.Id = 2584;
                //        break;
                //}

                if(ConfigurationManager.AppSettings["PATH_TP"] == "https://cis.tpondemand.com/api/v1/")
                {
                    this.Id = 2584;
                }
            }

            //private int GetProjectsIDbyName(string sdpApplication)
            //{
            //    //projects?where=Name eq "test2"
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
