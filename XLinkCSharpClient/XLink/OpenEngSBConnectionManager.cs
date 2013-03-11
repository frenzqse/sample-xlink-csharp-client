﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Web;

using Newtonsoft.Json;

using OOSourceCodeDomain;
using OpenEngSBCore;
using Org.Openengsb.Loom.CSharp.Bridge.Implementation;
using Org.Openengsb.Loom.CSharp.Bridge.Implementation.Common;
using Org.Openengsb.Loom.CSharp.Bridge.Interface;

using Org.Openengsb.XLinkCSharpClient.Model;

namespace Org.Openengsb.XLinkCSharpClient.XLink
{
    /// <summary>
    /// Manages the connect/disconnect to the OpenEngSB and the registration/deregistration to XLink.
    /// </summary>
    class OpenEngSBConnectionManager
    {
        /*Supplied program arguments*/
        private String xlinkServerURL;
        private String domainId;
        private String programname;

        /// <summary>
        /// Only possible OpenEngSBConnectionManager instance
        /// </summary>
        private static OpenEngSBConnectionManager instance = null;

        /// <summary>
        /// Flag indicating the connection status
        /// </summary>
        private bool connected = false;

        /// <summary>
        /// HostIP of the local system, used to identify the Host during an XLink call
        /// </summary>
        private String hostIp;

        /// <summary>
        /// Id of the registered OpenEngSB-Connector
        /// </summary>
        private String connectorUUID;

        /// <summary>
        /// XLinkUrlBlueprint received during XLinkRegistration
        /// </summary>
        private XLinkUrlBlueprint blueprint;

        /// <summary>
        /// Qualified Class Name to identify the used DomainModel at the OpenEngSB
        /// </summary>
        private string classNameOfOpenEngSBModel;

        /*XLink variables*/
        private static IOOSourceCodeDomainSoap11Binding ooSourceConnector;
        private static IDomainFactory factory;

        private OpenEngSBConnectionManager(String xlinkServerURL, String domainId, String programname, String hostIp, string classNameOfOpenEngSBModel)
        {
            this.xlinkServerURL = xlinkServerURL;
            this.domainId = domainId;
            this.programname = programname;
            this.connected = false;
            this.hostIp = hostIp;
            this.classNameOfOpenEngSBModel = classNameOfOpenEngSBModel;
        }	

        /// <summary>
        /// Initializes the Connectors only instance.
        /// </summary>
        /// <param name="xlinkBaseUrl">Link to OpenEngSB server</param>
        /// <param name="hostIp">IP of the local host</param>
        public static void initInstance(String xlinkBaseUrl,
                String domainId, String programname,
                String hostIp, string classNameOfOpenEngSBModel)
        {
            instance = new OpenEngSBConnectionManager(xlinkBaseUrl, domainId,
                    programname, hostIp, classNameOfOpenEngSBModel);
        }

        /// <summary>
        /// Returns the Connectors only instance.
        /// </summary>
        /// <returns>Connectors only instance</returns>
        public static OpenEngSBConnectionManager getInstance()
        {
            if (instance == null)
            {
                Console.WriteLine("getInstance():OpenEngSBConnectionManager was not initialized.");
            }
            return instance;
        }

        /// <summary>
        /// Creates/Registers the connector at the OpenEngSB and registers the connector to XLink
        /// </summary>
        public void connectToOpenEngSbWithXLink() 
        {
            outputLine("Trying to connect to OpenEngSB and XLink...");
            ooSourceConnector = new OOSourceCodeConnector();
            factory = DomainFactoryProvider.GetDomainFactoryInstance("3.0.0", xlinkServerURL, ooSourceConnector, new ForwardDefaultExceptionHandler());
            try
            {
                connectorUUID = factory.CreateDomainService(domainId);
            }
            catch (Exception e)
            {
                outputLine("An error happened.");
            }
            factory.RegisterConnector(connectorUUID, domainId);
            blueprint = factory.ConnectToXLink(connectorUUID, hostIp, programname, initModelViewRelation());
            connected = true;
            outputLine("Connecting done.");
        }

        /// <summary>
        /// Unregisters the connector from XLink and removes it from the OpenEngSB
        /// </summary>
        public void disconnect()
        {
            outputLine("Disconnecting from OpenEngSB and XLink...");
            factory.DisconnectFromXLink(connectorUUID, hostIp);
            factory.UnRegisterConnector(connectorUUID);
            factory.DeleteDomainService(connectorUUID);
            factory.StopConnection(connectorUUID);
            outputLine("Disconnected.");
        }

        private void outputLine(string line)
        {
            Console.WriteLine(line);
        }

        public bool isConnected()
        {
            return connected;
        }

        /// <summary>
        /// Creates the Array of Model/View relations, offered by the Tool, for XLink
        /// </summary>
        private ModelToViewsTuple[] initModelViewRelation()
        {
            ModelToViewsTuple[] modelsToViews
                = new ModelToViewsTuple[1];
            Dictionary<String, String> descriptions = new Dictionary<String, String>();
            descriptions.Add("en", "This view opens the values in a C# SourceCode viewer.");
            descriptions.Add("de", "Dieses Tool öffnet die Werte in einem C# SourceCode viewer.");

            OpenEngSBCore.XLinkConnectorView[] views = new OpenEngSBCore.XLinkConnectorView[1];
            views[0] = (new OpenEngSBCore.XLinkConnectorView() { name = "C# SourceCode View", viewId = Program.viewId, descriptions = descriptions.ConvertMap<entry3>() });
            modelsToViews[0] =
                    new ModelToViewsTuple()
                    {
                        description = new ModelDescription() { modelClassName = classNameOfOpenEngSBModel, versionString = "3.0.0.SNAPSHOT" },
                        views = views
                    };
            return modelsToViews;
        }

        /// <summary>
        /// TODO TBW
        /// </summary>
        /// <param name="file"></param>
        public void createXLink(WorkingDirectoryFile file)
        {
            if (!connected)
            {
                outputLine("Error while creating XLink. No connection to OpenEngSB.");
                return;
            }
            ModelDescription modelInformation = blueprint.viewToModels.ConvertMap<String, ModelDescription>()[Program.viewId];

    	    /*Note that only the target class SQLCreate is allowed */
            if (!modelInformation.modelClassName.Equals(classNameOfOpenEngSBModel))
            {
                outputLine("Error: Defined ModelClass '"+ classNameOfOpenEngSBModel + "' for view, from OpenEngSB, is not supported by this software program.");
                return;
            }

            String completeUrl = blueprint.baseUrl;
            completeUrl += "&" + blueprint.keyNames.modelClassKeyName + "=" + HttpUtility.UrlEncode(modelInformation.modelClassName);
            completeUrl += "&" + blueprint.keyNames.modelVersionKeyName + "=" + HttpUtility.UrlEncode(modelInformation.versionString);
            completeUrl += "&" + blueprint.keyNames.contextIdKeyName + "=" + HttpUtility.UrlEncode(Program.openengsbContext);      

            string objectString = convertWorkingDirectoryFileToJSON(file);
            outputLine(objectString);
            completeUrl += "&" + blueprint.keyNames.identifierKeyName + "=" + HttpUtility.UrlEncode(objectString);

            Clipboard.SetText(completeUrl);
        }

        /// <summary>
        /// TODO TBW
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        private string convertWorkingDirectoryFileToJSON(WorkingDirectoryFile file)
        {
            OOClass ooClassOfFile = LinkingUtils.convertWorkingDirectoryFileToOpenEngSBModel(file);
            string output = JsonConvert.SerializeObject(ooClassOfFile);
            return output;
        }
    }
}
