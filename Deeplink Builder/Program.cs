using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Deeplink_Builder
{
    class Program
    {
        static void Main(string[] args)
        {
            //var wauid = 7d7736b2 - 9981 - 49e7 - a2e8 - 8e2e978f0da1
            //var FC_UserName = "ASSERO_DEMO_API_EXTERNAL";
            //var FC_Password = "24drowssaPAsisihT";
            //The current example only works with app.form.com if you are
            //using Single Sign On then you will need to update the WSDL 
            //address in the service reference and refresh each service.
            Console.Write("Please enter your form.com domain (Default: app.form.com): ");
            var fcDomain = Console.ReadLine();
            fcDomain = String.IsNullOrEmpty(fcDomain)?"app.form.com" : fcDomain;
            if (!fcDomain.Equals("app.form.com")) {
                Console.WriteLine("WARNING");
                Console.WriteLine();
                Console.WriteLine("All API calls are domain specific and this example requires that users");
                Console.WriteLine("not on app.form.com must replace the domain in the service references");
                Console.WriteLine();
                Console.WriteLine("Additionally, you must change the fcDomain variable below");
                Console.WriteLine();
                Console.WriteLine("Please note, while you may continue with the test. It will not work");
                Console.WriteLine();
                Console.WriteLine();
            }
            //Reset to configured domain
            fcDomain = "app.form.com";

            Console.Write("Please enter your API Username: ");
            var FC_UserName = Console.ReadLine();
            Console.Write("Please enter your API Password: ");
            var FC_Password = Console.ReadLine();
            Console.Write("Please enter a WAUID: ");
            var WAUID = Console.ReadLine();
            
            //Program Variables
            string taskDefID ="-1";
            string formID="-1";
            long configDMID;
            
            //Default group for "All Records" may be updated in the future
            long configDMGroup = -1;
            long portalID;
            var deepLinkURL = "";

            //This inspection identifier is standard accross the current definition of all forms
            string WAUIDAnswerIdentifier = "InspectionID";
            //Establish required API Connection Endpoints
            //Form Design Service used to get form structure
            var designSvc = new FormDesignService.FormDesignManagementServiceClient();
            designSvc.ClientCredentials.UserName.UserName = FC_UserName;
            designSvc.ClientCredentials.UserName.Password = FC_Password;

            //Form Result Service used to get the respondent id for the offline link
            var resultSvc = new FormResultService.FormResultManagementServiceClient();
            resultSvc.ClientCredentials.UserName.UserName = FC_UserName;
            resultSvc.ClientCredentials.UserName.Password = FC_Password;

            //Task Service to get the task id for the offline link
            var taskSvc = new TaskService.TaskManagementServiceClient();
            taskSvc.ClientCredentials.UserName.UserName = FC_UserName;
            taskSvc.ClientCredentials.UserName.Password = FC_Password;

            //Data Model Service is used to determine the currently active form.
            var dmSvc = new DataModelService.DataModelsServiceClient();
            dmSvc.ClientCredentials.UserName.UserName = FC_UserName;
            dmSvc.ClientCredentials.UserName.Password = FC_Password;

            //Get Portal ID
            var cmService = new ContactManagerService.ContactsManagementServiceClient();
            cmService.ClientCredentials.UserName.UserName = FC_UserName;
            cmService.ClientCredentials.UserName.Password = FC_Password;
            //Get Contact Manager List
            var cmList = cmService.listContactManagers();

            var userInput = "";
           
            //Choose a contact manager
            Console.WriteLine("Contact Managers");
            for (var i = 0; i < cmList.Length; i++)
            {
                Console.WriteLine("\t" + (i + 1) + ".\t " + cmList[i].name);
            }
            Console.WriteLine();
            Console.Write("\nPlease choose a contact manager: ");

            userInput = Console.ReadLine();
            Console.WriteLine("\nCM Selected: " + cmList[Convert.ToInt64(userInput) - 1].name);

            portalID = cmList[Convert.ToInt64(userInput) - 1].id;
            //Clearing userinput for later use
            userInput = "";



            //Get Data Model Service
            var dmList = dmSvc.listModels();

            userInput = "";
            //Console.Clear();
            //Choose a contact manager
            Console.WriteLine("\nData Model\n");
            for (var i = 0; i < dmList.Length; i++)
            {
                Console.WriteLine("\t" + (i + 1) + ".\t " + dmList[i].name);
            }
            Console.WriteLine();
            Console.Write("\nPlease choose the configuration data model: ");

            userInput = Console.ReadLine();
            Console.WriteLine("\nData model Selected: " + dmList[Convert.ToInt64(userInput) - 1].name);
            configDMID = dmList[Convert.ToInt64(userInput) - 1].id;

            //Clearing userinput for later use
            userInput = "";

            //Determine active form
            Console.Write("\nScanning Configuration Datamodel and Determining Active Form");
            var allConfigRecords = dmSvc.getModelObjectsByFilter(configDMID, configDMGroup);
            Console.WriteLine(": " + allConfigRecords.Length + " records found");
            foreach (DataModelService.WSModelObject thisObject in allConfigRecords) {
                if (deepLinkURL.Equals(""))
                {
                    //Iterate through columns to get appropriate data
                    //Console.WriteLine("Records in allConfigRecord" + allConfigRecords.Length);
                    foreach (DataModelService.WSModelObjectEntry recordCol in thisObject.properties)
                    {
                        //Check for Task Definition ID
                        if (recordCol.key.Equals("TaskDefID"))
                        {
                            taskDefID = recordCol.value;
                        }
                        else
                        {
                            //Check for Task Form ID
                            if (recordCol.key.Equals("WorkflowSID"))
                            {
                                formID = recordCol.value;
                            }
                        }

                        if (formID.Equals("-1") || taskDefID.Equals("-1"))
                        {
                            //FormID and TaskDefID have not been discovered in this record yet.
                        }
                        else
                        {
                            //Valid configuration has been found
                            Console.Write("\nChecking FormID: " + formID + " and TaskID: " + taskDefID + " for inspection");
                            //Get filtered list of responses in the form
                            var responseFilter = new FormResultService.WSCriteriaCondition();
                            responseFilter.tag = WAUIDAnswerIdentifier;
                            responseFilter.value = WAUID;


                            var responseList = resultSvc.getRespondentsByCriteriaWithIncompletes(Convert.ToInt64(formID), responseFilter);
                            if (responseList.Length <= 0)
                            {
                                Console.WriteLine("\n\tNo matching inspection found in FormID " + formID + "\n");
                            }
                            else
                            {
                                long respondentID = -1;
                                foreach (var thisResponse in responseList)
                                {
                                    //If the response is complete then a link can not be generated.
                                    if (thisResponse.status.Equals(FormResultService.WSRespondentStatus.COMPLETE) || thisResponse.status.Equals(FormResultService.WSRespondentStatus.COMPLETE_MAIL))
                                    {
                                        //No action required for this record
                                    }
                                    else
                                    {
                                        if (deepLinkURL.Equals(""))
                                        {
                                            Console.WriteLine("\n\tRespondent " + thisResponse.respondentId + " is an open response\n");
                                            respondentID = thisResponse.respondentId;
                                            //get task defintion id
                                            var taskID = taskSvc.getTasksByRespondent(Convert.ToInt64(taskDefID), respondentID)[0].taskId;
                                            //Create Deep Link
                                            deepLinkURL = "https://launch.form.com/server/" + fcDomain + "/portal/" + portalID + "/sync/tasks/" + taskDefID + "/task_view/" + taskID + "/form";
                                            //Since a deeplink has been generated, stop iterating through responses
                                            break;
                                        }
                                    }
                                }
                                if (respondentID == -1)
                                {
                                    Console.WriteLine("\n\tNo matching inspection Found in FormID " + formID + '\n');
                                }
                            }
                            //Always break because this means the parsing of columns have been completed.
                            break;

                        }
                    }
                    formID = "-1";
                    taskDefID = "-1";
                } else
                {
                    Console.WriteLine(deepLinkURL);
                }

            }
            //If no deep link is found then form must already be completed or be in QA.
            if (deepLinkURL.Equals("")) {
                Console.WriteLine("No open inspection found matching the designated WAUID");
            }
            

            Console.WriteLine("Press any key to exit");
            Console.ReadKey();
        }
    }
}
