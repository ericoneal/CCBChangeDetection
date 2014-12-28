using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
using ESRI.ArcGIS.esriSystem;
using ESRI.ArcGIS.Geometry;
using ESRI.ArcGIS.SOESupport;

namespace CCBChangeDetection
{


    [ComVisible(true)]
    [Guid("416B0236-B0C1-47DF-8991-E87269ECC846")]
    [ClassInterface(ClassInterfaceType.None)]
    public class FeatureInfo : IXMLSerialize
    {

        public string UniqueFieldVal { get; set; }
        public string DiffType { get; set; }
        public string DiffDescript { get; set; }
        public string FID { get; set; }
        public double x { get; set; }
        public double y { get; set; }

        #region IXMLSerialize Members

        public void Serialize(IXMLSerializeData data)
        {
            data.TypeName = this.GetType().Name;
            data.TypeNamespaceURI = CCBChangeDetection.c_ns_soe;

            data.AddString("UniqueFieldVal", UniqueFieldVal);
            data.AddString("DiffType", DiffType);
            data.AddString("DiffDescript", DiffDescript);
            data.AddString("FID", FID);
            data.AddDouble("x", x);
            data.AddDouble("y", y);
        }

        public void Deserialize(IXMLSerializeData data)
        {
            int idx = FindMandatoryParam("UniqueFieldVal", data);
            this.UniqueFieldVal = data.GetString(idx);

            idx = FindMandatoryParam("DiffType", data);
            this.DiffType = data.GetString(idx);

            idx = FindMandatoryParam("DiffDescript", data);
            this.DiffDescript = data.GetString(idx);

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
    [Guid("8D7CF4B1-D915-4B31-B6BF-FB06FB9A3580")]
    [ClassInterface(ClassInterfaceType.None)]
    public class FeatureInfos : SerializableList<FeatureInfo>
    {
        public FeatureInfos(string namespaceURI)
            : base(namespaceURI)
        {
        }

    } //class CustomLayerInfos

}
