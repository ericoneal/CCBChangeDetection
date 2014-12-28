// Copyright 2012 ESRI
// 
// All rights reserved under the copyright laws of the United States
// and applicable international laws, treaties, and conventions.
// 
// You may freely redistribute and use this sample code, with or
// without modification, provided you include the original copyright
// notice and use restrictions.
// 
// See the use restrictions at <your ArcGIS install location>/DeveloperKit10.1/userestrictions.txt.
// 

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Runtime.InteropServices;

using ESRI.ArcGIS.esriSystem;
using ESRI.ArcGIS.Server;
using ESRI.ArcGIS.Geometry;
using ESRI.ArcGIS.Geodatabase;
using ESRI.ArcGIS.Carto;

using ESRI.ArcGIS.SOESupport;
using ESRI.ArcGIS.DataSourcesGDB;
using System.Data;
using System.IO;


//TODO: sign the project (project properties > signing tab > sign the assembly)
//      this is strongly suggested if the dll will be registered using regasm.exe <your>.dll /codebase


namespace CCBChangeDetection
{
    [ComVisible(true)]
    [Guid("ff21a501-5eb9-406a-aab1-29389d25b868")]
    [ClassInterface(ClassInterfaceType.None)]
    [ServerObjectExtension("MapServer",
        AllCapabilities = "GetCCBVersionDifferences,GetCCBFieldDifference",
        DefaultCapabilities = "GetCCBVersionDifferences,GetCCBFieldDifference",
        Description = "CCB Version Change Detection",
        DisplayName = "CCB Version Change Detection",
        //Properties = "Server=Venus;Instance=5264;Database=gisamfmb;Version=SDEMGR.CCBUpdates;User=sdemgr;Password=sdemgr;ParentVerion=SDEMGR.QA/QC",
        SupportsREST = false,
        SupportsSOAP = true)]


    public class CCBChangeDetection : IRequestHandler2, IServerObjectExtension, IObjectConstruct
    {
        private const string c_soe_name = "CCBChangeDetection";
        internal static string c_ns_soe = "http://examples.esri.com/schemas/CCBChangeDetection/1.0";
        internal static string c_ns_esri = "http://www.esri.com/schemas/ArcGIS/10.1";

        private IServerObjectHelper serverObjectHelper;
        private ServerLogger logger;
        private IPropertySet configProps;

        IRequestHandler2 reqHandler;

        string strServer, strInstance, strDatabase, strUser, strPasswd;

        IWorkspace workspaceCCBUpdates;
        IWorkspace workspaceQAQC;
        IFeatureLayer pLayerCHILDVersion;
        IFeatureLayer pLayerPARENTVersion;
        IFeatureLayer pFChangedFeatures;

        string strParentVersion;
        string strChildVersion = "";
        string strLayername = "";
        string strUniqueField = "";
        string strObservedField = "";



        public CCBChangeDetection()
        {

            SoapCapabilities soapCaps = new SoapCapabilities();
            soapCaps.AddMethod("GetCCBVersionDifferences", "GetCCBVersionDifferences");
            soapCaps.AddMethod("GetCCBFieldDifference", "GetCCBFieldDifference");


            logger = new ServerLogger();

            SoeSoapImpl soapImpl = new SoeSoapImpl(c_soe_name, soapCaps, HandleSoapMessage);
            reqHandler = (IRequestHandler2)soapImpl;


        }

        //IServerObjectExtension
        public void Init(IServerObjectHelper pSOH)
        {
           // System.Diagnostics.Debugger.Launch();
            serverObjectHelper = pSOH;
        }

        public void Shutdown()
        {
            serverObjectHelper = null;
        }


        //IObjectConstruct 
        public void Construct(IPropertySet props)
        {
            logger.LogMessage(ServerLogger.msgType.infoSimple, QualifiedMethodName(c_soe_name, "Construct"), -1, "Construct starting");

            configProps = props;
  
            logger.LogMessage(ServerLogger.msgType.infoSimple, QualifiedMethodName(c_soe_name, "Construct"), -1, "Construct finishing");
        }

        //IRequestHandler
        public byte[] HandleBinaryRequest(ref byte[] request)
        {
            throw new NotImplementedException();
        }

        public byte[] HandleBinaryRequest2(string Capabilities, ref byte[] request)
        {
            throw new NotImplementedException();
        }

        public string HandleStringRequest(string Capabilities, string request)
        {
            return reqHandler.HandleStringRequest(Capabilities, request);
        }

        public void HandleSoapMessage(IMessage reqMsg, IMessage respMsg)
        {
            string methodName = reqMsg.Name;
            bool bogusmethod = true;

            if (string.Compare(methodName, "GetCCBVersionDifferences", true) == 0)
            {
                bogusmethod = false;
                GetCCBVersionDifferences(reqMsg, respMsg);
            }


            if (string.Compare(methodName, "GetCCBFieldDifference", true) == 0)
            {
                bogusmethod = false;
                GetCCBFieldDifference(reqMsg, respMsg);
            }

            if (bogusmethod)
            {
                throw new ArgumentException("Method not supported: " + QualifiedMethodName(c_soe_name, methodName));
            }
            //else
            //    throw new ArgumentException("Method not supported: " + QualifiedMethodName(c_soe_name, methodName));
        }

        private string QualifiedMethodName(string soeName, string methodName)
        {
            return soeName + "." + methodName;
        }

        #region wrapperMethods

        private void GetCCBVersionDifferences(IMessage reqMsg, IMessage respMsg)
        {
            try
            {
                IXMLSerializeData reqParams = reqMsg.Parameters;
                strLayername = reqParams.GetString(FindParam("SDEFeatureClass", reqParams));
                strUniqueField = reqParams.GetString(FindParam("UniqueField", reqParams));
                strServer = reqParams.GetString(FindParam("SDEServer", reqParams));
                strDatabase = reqParams.GetString(FindParam("SDEDatabase", reqParams));
                strInstance = reqParams.GetString(FindParam("SDEInstance", reqParams));
                strParentVersion = reqParams.GetString(FindParam("SDEParentVersion", reqParams));
                strChildVersion = reqParams.GetString(FindParam("SDEChildVersion", reqParams));
                strUser = reqParams.GetString(FindParam("SDEUser", reqParams));
                strPasswd = reqParams.GetString(FindParam("SDEPassword", reqParams));

         

                workspaceCCBUpdates = SDEConnect(strChildVersion);
                workspaceQAQC = SDEConnect(strParentVersion);
                pLayerCHILDVersion = GetServicesLayer(workspaceCCBUpdates);
                pLayerPARENTVersion = GetServicesLayer(workspaceQAQC);
                pFChangedFeatures = MakeNewFeaturelayer(pLayerCHILDVersion,false);

                IFeatureLayer pFeatureLayer = doChangeDetection();
                IFeatureCursor pFeatureCursor = pFeatureLayer.FeatureClass.Search(null, false);
                IFeature pFeature = pFeatureCursor.NextFeature();

                string strATTACHMENT_NUMBER;
                string strDiffType;
                string strDiffDescript;
                string strFID;
                double dblX = 0;
                double dblY = 0;

                IPoint ppoint;
                FeatureInfos FeatureInfos = new FeatureInfos(c_ns_soe);
                while (pFeature != null)
                {
                    ppoint = pFeature.ShapeCopy as IPoint;
                    strATTACHMENT_NUMBER = pFeature.get_Value(pFeature.Fields.FindField(strUniqueField)).ToString() + "";
                    strDiffType = pFeature.get_Value(pFeature.Fields.FindField("DiffType")).ToString() + "";
                    strDiffDescript = pFeature.get_Value(pFeature.Fields.FindField("DiffDesc")).ToString() + "";
                    strFID = pFeature.get_Value(pFeature.Fields.FindField("FID")).ToString() + "";

                    dblX = ppoint.X;
                    dblY = ppoint.Y;

                    FeatureInfo featureinfo = new FeatureInfo();
                    featureinfo.UniqueFieldVal = strATTACHMENT_NUMBER;
                    featureinfo.FID = strFID;
                    featureinfo.DiffType = strDiffType;
                    featureinfo.DiffDescript = strDiffDescript;
                    featureinfo.x = dblX;
                    featureinfo.y = dblY;


                    FeatureInfos.Add(featureinfo);


                    pFeature = pFeatureCursor.NextFeature();
                }


                pFeatureCursor.Flush();
                pFeatureCursor = null;

                respMsg.Name = "GetLayerInfosResponse";
                respMsg.NamespaceURI = c_ns_soe;
                respMsg.Parameters.AddObject("Result", FeatureInfos);

                FeatureInfos = null;
                pFChangedFeatures = null;

                workspaceCCBUpdates = null;
                workspaceQAQC = null;
            }

            catch (Exception ex)
            {
               
            }
        }

        private void GetCCBFieldDifference(IMessage reqMsg, IMessage respMsg)
        {
            try
            {
                IXMLSerializeData reqParams = reqMsg.Parameters;
                strLayername = reqParams.GetString(FindParam("SDEFeatureClass", reqParams));
                strUniqueField = reqParams.GetString(FindParam("UniqueField", reqParams));
                strServer = reqParams.GetString(FindParam("SDEServer", reqParams));
                strDatabase = reqParams.GetString(FindParam("SDEDatabase", reqParams));
                strInstance = reqParams.GetString(FindParam("SDEInstance", reqParams));
                strParentVersion = reqParams.GetString(FindParam("SDEParentVersion", reqParams));
                strChildVersion = reqParams.GetString(FindParam("SDEChildVersion", reqParams));
                strUser = reqParams.GetString(FindParam("SDEUser", reqParams));
                strPasswd = reqParams.GetString(FindParam("SDEPassword", reqParams));
                strObservedField = reqParams.GetString(FindParam("ObservedField", reqParams));  


                workspaceCCBUpdates = SDEConnect(strChildVersion);
                workspaceQAQC = SDEConnect(strParentVersion);
                pLayerCHILDVersion = GetServicesLayer(workspaceCCBUpdates);
                pLayerPARENTVersion = GetServicesLayer(workspaceQAQC);
                pFChangedFeatures = MakeNewFeaturelayer(pLayerCHILDVersion, true);

                IFeatureLayer pFeatureLayer = doFieldChangeDetection();
                IFeatureCursor pFeatureCursor = pFeatureLayer.FeatureClass.Search(null, false);
                IFeature pFeature = pFeatureCursor.NextFeature();

                string strATTACHMENT_NUMBER;
                string ParentVal;
                string ChildVal;
                string strFID;
                double dblX = 0;
                double dblY = 0;

                IPoint ppoint;
                FieldInfos fieldinfos = new FieldInfos(c_ns_soe);
                while (pFeature != null)
                {
                    ppoint = pFeature.ShapeCopy as IPoint;
                    strATTACHMENT_NUMBER = pFeature.get_Value(pFeature.Fields.FindField(strUniqueField)).ToString() + "";
                    ParentVal = pFeature.get_Value(pFeature.Fields.FindField("ParentVal")).ToString() + "";
                    ChildVal = pFeature.get_Value(pFeature.Fields.FindField("ChildVal")).ToString() + "";
                    strFID = pFeature.get_Value(pFeature.Fields.FindField("FID")).ToString() + "";

                    dblX = ppoint.X;
                    dblY = ppoint.Y;

                    FieldInfo fieldinfo = new FieldInfo();
                    fieldinfo.UniqueFieldVal = strATTACHMENT_NUMBER;
                    fieldinfo.FID = strFID;
                    fieldinfo.ParentVal = ParentVal;
                    fieldinfo.ChildVal = ChildVal;
                    fieldinfo.x = dblX;
                    fieldinfo.y = dblY;


                    fieldinfos.Add(fieldinfo);


                    pFeature = pFeatureCursor.NextFeature();
                }


                pFeatureCursor.Flush();
                pFeatureCursor = null;

                respMsg.Name = "GetLayerInfosResponse";
                respMsg.NamespaceURI = c_ns_soe;
                respMsg.Parameters.AddObject("Result", fieldinfos);

                fieldinfos = null;
                pFChangedFeatures = null;

                workspaceCCBUpdates = null;
                workspaceQAQC = null;
            }

            catch (Exception ex)
            {

            }
        }


        #endregion wrapperMethods


        #region businessLogicMethods

        private IFeatureLayer doChangeDetection()
        {

            //Inserts
            IFIDSet fids = FindVersionDifferences(workspaceCCBUpdates, strChildVersion, strParentVersion, strLayername, esriDifferenceType.esriDifferenceTypeInsert);
            AddFeatures(pFChangedFeatures, fids, esriDifferenceType.esriDifferenceTypeInsert);
            fids.SetEmpty();

            //esriDifferenceTypeDeleteNoChange
            fids = FindVersionDifferences(workspaceCCBUpdates, strChildVersion, strParentVersion, strLayername, esriDifferenceType.esriDifferenceTypeDeleteNoChange);
            AddFeatures(pFChangedFeatures, fids, esriDifferenceType.esriDifferenceTypeDeleteNoChange);
            fids.SetEmpty();


            //esriDifferenceTypeDeleteUpdate
            fids = FindVersionDifferences(workspaceCCBUpdates, strChildVersion, strParentVersion, strLayername, esriDifferenceType.esriDifferenceTypeDeleteUpdate);
            AddFeatures(pFChangedFeatures, fids, esriDifferenceType.esriDifferenceTypeDeleteUpdate);
            fids.SetEmpty();

            //esriDifferenceTypeUpdateDelete
            fids = FindVersionDifferences(workspaceCCBUpdates, strChildVersion, strParentVersion, strLayername, esriDifferenceType.esriDifferenceTypeUpdateDelete);
            AddFeatures(pFChangedFeatures, fids, esriDifferenceType.esriDifferenceTypeUpdateDelete);
            fids.SetEmpty();


            //esriDifferenceTypeUpdateNoChange
            fids = FindVersionDifferences(workspaceCCBUpdates, strChildVersion, strParentVersion, strLayername, esriDifferenceType.esriDifferenceTypeUpdateNoChange);
            AddFeatures(pFChangedFeatures, fids, esriDifferenceType.esriDifferenceTypeUpdateNoChange);
            fids.SetEmpty();

            //esriDifferenceTypeUpdateUpdate
            fids = FindVersionDifferences(workspaceCCBUpdates, strChildVersion, strParentVersion, strLayername, esriDifferenceType.esriDifferenceTypeUpdateUpdate);
            AddFeatures(pFChangedFeatures, fids, esriDifferenceType.esriDifferenceTypeUpdateUpdate);
            fids.SetEmpty();


            return pFChangedFeatures;


        }



        private IFeatureLayer doFieldChangeDetection()
        {


            //Inserts
            IFIDSet fids = FindVersionDifferences(workspaceCCBUpdates, strChildVersion, strParentVersion, strLayername, esriDifferenceType.esriDifferenceTypeInsert);
            AddFeatures2(pFChangedFeatures, fids, esriDifferenceType.esriDifferenceTypeInsert);
            fids.SetEmpty();

            //esriDifferenceTypeDeleteNoChange
            fids = FindVersionDifferences(workspaceCCBUpdates, strChildVersion, strParentVersion, strLayername, esriDifferenceType.esriDifferenceTypeDeleteNoChange);
            AddFeatures2(pFChangedFeatures, fids, esriDifferenceType.esriDifferenceTypeDeleteNoChange);
            fids.SetEmpty();


            //esriDifferenceTypeDeleteUpdate
            fids = FindVersionDifferences(workspaceCCBUpdates, strChildVersion, strParentVersion, strLayername, esriDifferenceType.esriDifferenceTypeDeleteUpdate);
            AddFeatures2(pFChangedFeatures, fids, esriDifferenceType.esriDifferenceTypeDeleteUpdate);
            fids.SetEmpty();

            //esriDifferenceTypeUpdateDelete
            fids = FindVersionDifferences(workspaceCCBUpdates, strChildVersion, strParentVersion, strLayername, esriDifferenceType.esriDifferenceTypeUpdateDelete);
            AddFeatures2(pFChangedFeatures, fids, esriDifferenceType.esriDifferenceTypeUpdateDelete);
            fids.SetEmpty();


            //esriDifferenceTypeUpdateNoChange
            fids = FindVersionDifferences(workspaceCCBUpdates, strChildVersion, strParentVersion, strLayername, esriDifferenceType.esriDifferenceTypeUpdateNoChange);
            AddFeatures2(pFChangedFeatures, fids, esriDifferenceType.esriDifferenceTypeUpdateNoChange);
            fids.SetEmpty();

            //esriDifferenceTypeUpdateUpdate
            fids = FindVersionDifferences(workspaceCCBUpdates, strChildVersion, strParentVersion, strLayername, esriDifferenceType.esriDifferenceTypeUpdateUpdate);
            AddFeatures2(pFChangedFeatures, fids, esriDifferenceType.esriDifferenceTypeUpdateUpdate);
            fids.SetEmpty();


            return pFChangedFeatures;


        }



        private void AddFeatures2(IFeatureLayer pChangedFeatures, IFIDSet fids, esriDifferenceType difftype)
        {
            IFeature pFeatureParent;
            IFeature pFeatureChild;
            IFeatureCursor pFCurOutPoints = pChangedFeatures.Search(null, false);
            IFeatureBuffer pFBuffer = pChangedFeatures.FeatureClass.CreateFeatureBuffer();
            pFCurOutPoints = pChangedFeatures.FeatureClass.Insert(true);



            fids.Reset();


            int iFid = 0;
            fids.Next(out iFid);


            while (iFid != -1)
            {

                try
                {

                    pFeatureParent = pLayerPARENTVersion.FeatureClass.GetFeature(iFid);
                    pFeatureChild = pLayerCHILDVersion.FeatureClass.GetFeature(iFid);


                    pFBuffer.Shape = pFeatureChild.ShapeCopy;
                    for (int g = 1; g <= pFeatureChild.Fields.FieldCount - 1; g++)
                    {
                        pFBuffer.set_Value(g, pFeatureChild.get_Value(g));
                    }

                    string NewVal = pFeatureChild.get_Value(pFeatureChild.Fields.FindField(strObservedField)).ToString();
                    string OldVal = pFeatureParent.get_Value(pFeatureParent.Fields.FindField(strObservedField)).ToString();

                    //May need to get QAQC OID here instead...
                    pFBuffer.set_Value(pFBuffer.Fields.FindField("FID"), iFid);
                    pFBuffer.set_Value(pFBuffer.Fields.FindField("ParentVal"), OldVal);
                    pFBuffer.set_Value(pFBuffer.Fields.FindField("ChildVal"), NewVal);

                    pFCurOutPoints.InsertFeature(pFBuffer);
                }

                catch { }

                fids.Next(out iFid);

            }
        }



        private DataTable ConvertLayer2DataTable(IFeatureLayer pFeatureLayer)
        {
            try
            {
                DataTable tmpDT = new DataTable("Feature");
                DataColumn column;
                List<string> lstColumns = new List<string>();


                ITable pTable = pFeatureLayer.FeatureClass as ITable;
                IFields pFields = pTable.Fields;
                ICursor pCur = pTable.Search(null, false);


                for (int c = 0; c <= pFeatureLayer.FeatureClass.Fields.FieldCount - 1; c++)
                {


                    column = new DataColumn();
                    column.ColumnName = pFields.Field[c].Name;

                    if (pFields.Field[c].Type == esriFieldType.esriFieldTypeString)
                    {
                        column.DataType = System.Type.GetType("System.String");
                    }
                    if (pFields.Field[c].Type == esriFieldType.esriFieldTypeInteger)
                    {
                        column.DataType = System.Type.GetType("System.Int32");
                    }
                    if (pFields.Field[c].Type == esriFieldType.esriFieldTypeDouble)
                    {
                        column.DataType = System.Type.GetType("System.Double");
                    }
                    if (pFields.Field[c].Type == esriFieldType.esriFieldTypeDate)
                    {
                        column.DataType = System.Type.GetType("System.String");
                    }
                    if (pFields.Field[c].Type == esriFieldType.esriFieldTypeSingle)
                    {
                        column.DataType = System.Type.GetType("System.Single");
                    }
                    //if (pFields.Field[c].Type == esriFieldType.esriFieldTypeBlob)
                    //{
                    //    column.DataType = System.Type.GetType("System.String");
                    //}
                    //if (pFields.Field[c].Type == esriFieldType.esriFieldTypeOID)
                    //{
                    //    column.DataType = System.Type.GetType("System.Int64");
                    //}
                    if (pFields.Field[c].Type == esriFieldType.esriFieldTypeSmallInteger)
                    {
                        column.DataType = System.Type.GetType("System.Int32");
                    }
                    if (pFields.Field[c].Type == esriFieldType.esriFieldTypeDate)
                    {
                        column.DataType = System.Type.GetType("System.DateTime");
                    }
                    column.ReadOnly = false;

                    tmpDT.Columns.Add(column);
                    lstColumns.Add(pFields.Field[c].Name);



                }

                IRow pRow = pCur.NextRow();
                DataRow newRow;
                while (pRow != null)
                {
                    newRow = tmpDT.NewRow();
                    newRow.BeginEdit();

                    foreach (string strFieldName in lstColumns)
                    {
                        int l = pRow.Fields.FindField(strFieldName);
                        newRow[strFieldName] = pRow.get_Value(l);

                    }
                    newRow.EndEdit();
                    tmpDT.Rows.Add(newRow);
                    tmpDT.AcceptChanges();

                    pRow = pCur.NextRow();
                }



                return tmpDT;
            }
            catch (Exception ex)
            {
                return null;
            }


        }

        private void AddFeatures(IFeatureLayer pChangedFeatures, IFIDSet fids, esriDifferenceType difftype)
        {
            IFeature pFeature;
            IFeatureCursor pFCurOutPoints = pChangedFeatures.Search(null, false);
            IFeatureBuffer pFBuffer = pChangedFeatures.FeatureClass.CreateFeatureBuffer();
            pFCurOutPoints = pChangedFeatures.FeatureClass.Insert(true);



            fids.Reset();


            int iFid = 0;
            fids.Next(out iFid);
           

            while (iFid != -1)
            {
              

                if (difftype.ToString().ToUpper().Contains("DELETE"))
                {
                    pFeature = pLayerPARENTVersion.FeatureClass.GetFeature(iFid);
                }
                else
                {
                    pFeature = pLayerCHILDVersion.FeatureClass.GetFeature(iFid);
                }

                pFBuffer.Shape = pFeature.ShapeCopy;
                for (int g = 1; g <= pFeature.Fields.FieldCount - 1; g++)
                {
                    pFBuffer.set_Value(g, pFeature.get_Value(g));
                }

                //May need to get QAQC OID here instead...
                pFBuffer.set_Value(pFBuffer.Fields.FindField("FID"), iFid);
                pFBuffer.set_Value(pFBuffer.Fields.FindField("DiffType"), difftype.ToString());
                pFBuffer.set_Value(pFBuffer.Fields.FindField("DiffDesc"), DiffTypeLookup(difftype));

                pFCurOutPoints.InsertFeature(pFBuffer);

                
                fids.Next(out iFid);

            }
        }

        private IFeatureLayer MakeNewFeaturelayer(IFeatureLayer pFeatureLayer, bool FieldDiffFields)
        {

            IWorkspaceFactory2 pWorkspaceFactory = new InMemoryWorkspaceFactoryClass();
            IWorkspaceName2 pWorkspaceName = pWorkspaceFactory.Create("", "MyInMemoryworkspace", null, 0) as IWorkspaceName2;

            IName PName = pWorkspaceName as IName;

            IFeatureWorkspace workspace = PName.Open() as IFeatureWorkspace;
            UID CLSID = new UID();
            CLSID.Value = "esriGeodatabase.Feature";

            IFields pFields = new FieldsClass();
            IFieldsEdit pFieldsEdit = pFields as IFieldsEdit;

            pFieldsEdit.FieldCount_2 = pFeatureLayer.FeatureClass.Fields.FieldCount + 3;



            IGeoDataset geoDataset = pFeatureLayer as IGeoDataset;


            IGeometryDef pGeomDef = new GeometryDef();
            IGeometryDefEdit pGeomDefEdit = pGeomDef as IGeometryDefEdit;
            pGeomDefEdit.GeometryType_2 = esriGeometryType.esriGeometryPoint;
            pGeomDefEdit.SpatialReference_2 = geoDataset.SpatialReference;



            IField pField;
            IFieldEdit pFieldEdit;

            for (int i = 0; i <= pFeatureLayer.FeatureClass.Fields.FieldCount - 1; i++)
            {
                pField = pFeatureLayer.FeatureClass.Fields.Field[i];
                pFieldEdit = pField as IFieldEdit;
                pFieldsEdit.set_Field(i, pFieldEdit);
            }

            int iFieldIndex = pFeatureLayer.FeatureClass.Fields.FieldCount;


            pField = new FieldClass();
            pFieldEdit = pField as IFieldEdit;
            pFieldEdit.AliasName_2 = "FID";
            pFieldEdit.Name_2 = "FID";
            pFieldEdit.Type_2 = esriFieldType.esriFieldTypeString;
            pFieldsEdit.set_Field(iFieldIndex, pFieldEdit);
            iFieldIndex++;

            if (FieldDiffFields)
            {
                pField = new FieldClass();
                pFieldEdit = pField as IFieldEdit;
                pFieldEdit.AliasName_2 = "ParentVal";
                pFieldEdit.Name_2 = "ParentVal";
                pFieldEdit.Type_2 = esriFieldType.esriFieldTypeString;
                pFieldsEdit.set_Field(iFieldIndex, pFieldEdit);
                iFieldIndex++;



                pField = new FieldClass();
                pFieldEdit = pField as IFieldEdit;
                pFieldEdit.AliasName_2 = "ChildVal";
                pFieldEdit.Name_2 = "ChildVal";
                pFieldEdit.Type_2 = esriFieldType.esriFieldTypeString;
                pFieldsEdit.set_Field(iFieldIndex, pFieldEdit);
                iFieldIndex++;
            }

            else
            {
                pField = new FieldClass();
                pFieldEdit = pField as IFieldEdit;
                pFieldEdit.AliasName_2 = "DiffType";
                pFieldEdit.Name_2 = "DiffType";
                pFieldEdit.Type_2 = esriFieldType.esriFieldTypeString;
                pFieldsEdit.set_Field(iFieldIndex, pFieldEdit);
                iFieldIndex++;



                pField = new FieldClass();
                pFieldEdit = pField as IFieldEdit;
                pFieldEdit.AliasName_2 = "DiffDesc";
                pFieldEdit.Name_2 = "DiffDesc";
                pFieldEdit.Type_2 = esriFieldType.esriFieldTypeString;
                pFieldsEdit.set_Field(iFieldIndex, pFieldEdit);
                iFieldIndex++;
            }

            string strFCName = System.IO.Path.GetFileNameWithoutExtension(System.IO.Path.GetRandomFileName());
            char[] chars = strFCName.ToCharArray();
            if (Char.IsDigit(chars[0]))
            {
                strFCName = strFCName.Remove(0, 1);
            }

            IFeatureLayer pFlayer = new FeatureLayerClass();
            pFlayer.FeatureClass = workspace.CreateFeatureClass(strFCName, pFieldsEdit, CLSID, null, esriFeatureType.esriFTSimple, "SHAPE", "");


            return pFlayer;
        }

        private IFIDSet FindVersionDifferences(IWorkspace workspace, String childVersionName, String parentVersionName, String tableName, esriDifferenceType differenceType)
        {
            // Get references to the child and parent versions.
            IVersionedWorkspace versionedWorkspace = (IVersionedWorkspace)workspace;
            IEnumVersionInfo pEnumVerions = versionedWorkspace.Versions;

            pEnumVerions.Reset();
            IVersionInfo pVersion = pEnumVerions.Next();

            while (pVersion != null)
            {
                //Console.WriteLine(pVersion.VersionName);
                pVersion = pEnumVerions.Next();
            }

            IVersion childVersion = versionedWorkspace.FindVersion(childVersionName);
            IVersion parentVersion = versionedWorkspace.FindVersion(parentVersionName);

            // Cast to the IVersion2 interface to find the common ancestor.
            IVersion2 childVersion2 = (IVersion2)childVersion;
            IVersion commonAncestorVersion = childVersion2.GetCommonAncestor
              (parentVersion);

            childVersion.RefreshVersion();
            parentVersion.RefreshVersion();
            childVersion2.RefreshVersion();

            // Cast the child version to IFeatureWorkspace and open the table.
            IFeatureWorkspace childFWS = (IFeatureWorkspace)childVersion;
            ITable childTable = childFWS.OpenTable(tableName);

            // Cast the common ancestor version to IFeatureWorkspace and open the table.
            IFeatureWorkspace commonAncestorFWS = (IFeatureWorkspace)
              commonAncestorVersion;
            ITable commonAncestorTable = commonAncestorFWS.OpenTable(tableName);

            // Cast to the IVersionedTable interface to create a difference cursor.
            IVersionedTable versionedTable = (IVersionedTable)childTable;
            
            IDifferenceCursor differenceCursor = versionedTable.Differences
              (commonAncestorTable, differenceType, null);

            // Create output variables for the IDifferenceCursor.Next method and a FID set.
            IFIDSet fidSet = new FIDSetClass();
            IRow differenceRow = null;
            int objectID = -1;

            // Step through the cursor, showing the ID of each modified row.
            differenceCursor.Next(out objectID, out differenceRow);
            while (objectID != -1)
            {
                fidSet.Add(objectID);
                differenceCursor.Next(out objectID, out differenceRow);
            }

            fidSet.Reset();
            return fidSet;
        }

        private IFeatureLayer GetServicesLayer(IWorkspace psdework)
        {
            IWorkspaceFactory2 pWorkFact = new SdeWorkspaceFactoryClass();
            IFeatureWorkspace pFWorkspace = psdework as IFeatureWorkspace;

            IFeatureClass pFClass = pFWorkspace.OpenFeatureClass(strLayername);


            IFeatureLayer pFLayer = new FeatureLayer();
            pFLayer.FeatureClass = pFClass;
            return pFLayer;

        }

        private IWorkspace SDEConnect(string strVersion)
        {
            ISetDefaultConnectionInfo2 pConnectionInfo;
            IPropertySet ppropset;
            IWorkspaceFactory psdefact;


            ppropset = new PropertySet();
            ppropset.SetProperty("SERVER", strServer);
            ppropset.SetProperty("INSTANCE", strInstance);
            ppropset.SetProperty("DATABASE", strDatabase);
            ppropset.SetProperty("VERSION", strVersion);
            ppropset.SetProperty("USER", strUser);
            ppropset.SetProperty("PASSWORD", strPasswd);

            //ppropset = new PropertySet();
            //ppropset.SetProperty("SERVER", "venus");
            //ppropset.SetProperty("INSTANCE", "5264");
            //ppropset.SetProperty("DATABASE", "gisamfmb");
            //ppropset.SetProperty("VERSION", strVersion);
            //ppropset.SetProperty("USER", "sdemgr");
            //ppropset.SetProperty("PASSWORD", "sdemgr");


            //'open the sde database to access the datasets
            psdefact = new SdeWorkspaceFactory();
            pConnectionInfo = psdefact as ISetDefaultConnectionInfo2;
            return psdefact.Open(ppropset, 0);


        }

        private string DiffTypeLookup(esriDifferenceType difftype)
        {
            string g = difftype.ToString();
            switch (difftype.ToString())
            {
                case "esriDifferenceTypeInsert":
                    return "Row was inserted in this version.";
                case "esriDifferenceTypeDeleteNoChange":
                    return "Row has been deleted in this version and not changed in the " + strParentVersion + " version.";
                case "esriDifferenceTypeUpdateNoChange":
                    return "Row has been updated in this version and not changed in the " + strParentVersion + " version.";
                case "esriDifferenceTypeUpdateUpdate":
                    return "Row has been updated in both versions.";
                case "esriDifferenceTypeUpdateDelete":
                    return "Row has been updated in this version but deleted in the " + strParentVersion + " version.";
                case "esriDifferenceTypeDeleteUpdate":
                    return "Row has been deleted in this version but updated in the " + strParentVersion + " version.";
            }

            return null;

        }



        #endregion businessLogicMethods


        private int FindParam(string parameterName, IXMLSerializeData msgParams)
        {
            int idx = msgParams.Find(parameterName);
            if (idx == -1)
                throw new ArgumentNullException(parameterName);
            return idx;
        }


    } //class 
}
