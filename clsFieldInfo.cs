﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
using ESRI.ArcGIS.esriSystem;
using ESRI.ArcGIS.Geometry;
using ESRI.ArcGIS.SOESupport;

namespace CCBChangeDetection
{


    [ComVisible(true)]
    [Guid("D6789F27-45C4-44C2-B4B1-EE34E128F418")]
    [ClassInterface(ClassInterfaceType.None)]
    public class FieldInfo : IXMLSerialize
    {

        public string UniqueFieldVal { get; set; }
        public string ParentVal { get; set; }
        public string ChildVal { get; set; }
        public string FID { get; set; }
        public double x { get; set; }
        public double y { get; set; }

        #region IXMLSerialize Members

        public void Serialize(IXMLSerializeData data)
        {
            data.TypeName = this.GetType().Name;
            data.TypeNamespaceURI = CCBChangeDetection.c_ns_soe;

            data.AddString("UniqueFieldVal", UniqueFieldVal);
            data.AddString("ParentVal", ParentVal);
            data.AddString("ChildVal", ChildVal);
            data.AddString("FID", FID);
            data.AddDouble("x", x);
            data.AddDouble("y", y);
        }

        public void Deserialize(IXMLSerializeData data)
        {
            int idx = FindMandatoryParam("UniqueFieldVal", data);
            this.UniqueFieldVal = data.GetString(idx);

            idx = FindMandatoryParam("ParentVal", data);
            this.ParentVal = data.GetString(idx);

            idx = FindMandatoryParam("ChildVal", data);
            this.ChildVal = data.GetString(idx);

            idx = FindMandatoryParam("FID", data);
            this.FID = data.GetString(idx);

            idx = FindMandatoryParam("x", data);
            this.x = data.GetDouble(idx);

            idx = FindMandatoryParam("y", data);
            this.y = data.GetDouble(idx);
        }

        #endregion

        private int FindMandatoryParam(string fieldName, IXMLSerializeData data)
        {
            int idx = data.Find(fieldName);
            if (idx == -1)
                throw new MissingMandatoryFieldException(fieldName);
            return idx;
        }

        internal class MissingMandatoryFieldException : Exception
        {
            internal MissingMandatoryFieldException(string fieldName) : base("Missing mandatory field: " + fieldName) { }
        }

    }

    [ComVisible(true)]
    [Guid("300DFC0F-46DC-4BB5-96EA-AA665E426A3D")]
    [ClassInterface(ClassInterfaceType.None)]
    public class FieldInfos : SerializableList<FieldInfo>
    {
        public FieldInfos(string namespaceURI)
            : base(namespaceURI)
        {
        }

    } //class CustomLayerInfos

}
