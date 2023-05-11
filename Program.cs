using CIM.Model;
using CIMParser;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace CIMImport
{
    internal class Program
    {
        //log and exception handling to be added in detail
        //private static CIMAdapter adapter = new CIMAdapter();
        //private static Delta nmsDelta = null;
        private static string cimfilePath = "";
        private static string coorfilePath = "";
        private static readonly bool clearAllTables = true;
        private static readonly bool showhypenforna = true;
        private static void Main(string[] args)
        {
            //var m = typeof(CIM_ACLineSegment);
            string mynamespace = System.Reflection.Assembly.GetExecutingAssembly().EntryPoint.DeclaringType.Namespace; //CIMImport
            //Type m = Type.GetType(mynamespace + ".Properties." + pp1.FirstOrDefault());
            //List<string> mm = m.GetProperties().Where(p => p.CanWrite == true).Select(p => p.Name).ToList();

            //Type type = Type.GetType("MyApplication.Action");
            //if (type == null)throw new Exception("Type not found.");
            //var instance = Activator.CreateInstance(type); //or
            //var newClass = System.Reflection.Assembly.GetAssembly(type).CreateInstance("MyApplication.Action");

            Argument.Parse(args, ref cimfilePath, ref coorfilePath); //, ref topology, ref model);

            string log;

            using (FileStream fs = File.Open(cimfilePath, FileMode.Open))
            {
                log = string.Empty;
                System.Globalization.CultureInfo culture = Thread.CurrentThread.CurrentCulture;
                Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("en-US");
                try
                {
                    Stopwatch s1 = Stopwatch.StartNew();

                    CIMModelLoaderResult modelLoadResult = CIMModelLoader.LoadCIMXMLModel(fs, cimfilePath, out CIMModel cimModel); //ProfileManager.Namespace
                    s1.Stop();
                    Console.WriteLine(s1.Elapsed);
                    Dictionary<string, object> mydictuniquecols = new Dictionary<string, object>();
                    //Dictionary<string, object> mydictallobj2 = new Dictionary<string, object>();
                    if (modelLoadResult.Success) //load successfull
                    {
                        //SortedDictionary<string, CIMObject> allwindings = cimModel.GetAllObjectsOfType(CIMConstants.TypeNameTransformerWinding);

                        //cimModel.FinishModelMap();
                        //var aa = cimModel.GetObjectByID("_0036C0FD-30BA-4af5-96D3-0F20113AF5D8");
                        //var bb = cimModel.GetObjectByID("_F98FC9FD-E64D-4a2e-96C1-DB2829EDB45C");
                        string detail = cimModel.ToString();
                        Dictionary<string, List<Dictionary<string, string>>> EquipIdtoTermNodes = FindTermNodesFromEquips(cimModel);
                        //check for debug purposes
                        List<KeyValuePair<string, List<Dictionary<string, string>>>> morethan2endeddevices = EquipIdtoTermNodes.Where(x => x.Value.Count > 2).ToList();
                        List<KeyValuePair<string, List<Dictionary<string, string>>>> singleendeddevices = EquipIdtoTermNodes.Where(x => x.Value.Count == 1).ToList();
                        List<KeyValuePair<string, List<Dictionary<string, string>>>> bothendnulldevices = EquipIdtoTermNodes.Where(x => x.Value.Count == 0).ToList();
                        Console.WriteLine($"morethan2ended {morethan2endeddevices.Count}, singlended {singleendeddevices.Count} and bothsideempty {bothendnulldevices.Count} devices found.");
                        //transformer to winding or transformer to transformerend translation
                        Dictionary<string, Dictionary<string, Dictionary<string, string>>> TrafotoWindings = FindWindingsFromTrafo(cimModel);

                        using (cim_import_dbEntities db = new cim_import_dbEntities())
                        {
                            db.Configuration.AutoDetectChangesEnabled = false; //for improving the performance
                            db.Configuration.ValidateOnSaveEnabled = false; //for improving the performance

                            Console.WriteLine($"{cimfilePath} load successful");
                            //Dictionary<string, CIMObject> mydictallobj = cimModel.ModelMap.SelectMany(x => x.Value).ToDictionary(x => x.Key, x => x.Value);
                            //Dictionary<string, Dictionary<string, object>> EquipTermNodes = new Dictionary<string, Dictionary<string, object>>();

                            Type p1 = typeof(cim_import_dbEntities);
                            List<string> pp1 = p1.GetProperties().Where(p => p.CanWrite == true).Select(p => p.Name).ToList();


                            if (clearAllTables == true)
                            {
                                //CIM tables no more need cleaning as it contains the importid as unique column across all rows, but DGM tables need cleaning before importing data
                                //cleaning all tables not required as each table row contains model id unique
                                //loop through each table and clear, except cim_model, because in few some files some tables may not be present and will remain of previous run output
                                foreach (string tablename in pp1)
                                {
                                    //if (tablename == "CIM_Model") continue; //dont clear cim_model, as it keeps history of import detail
                                    db.Database.ExecuteSqlCommand($"TRUNCATE TABLE [{tablename}];"); /*SET FOREIGN_KEY_CHECKS = 0;*/
                                }
                                db.SaveChanges();
                            }

                            List<string> outside = new List<string>();
                            try
                            {
                                /*first fill in CIM_Model, ONLY the highest id having enddt and terminatedt 3000 and rest all are null*/
                                foreach (CIM_Model eachcm in db.CIM_Model)
                                {
                                    eachcm.EndDT = null;
                                    eachcm.TerminateDT = null;// eachcm.CreateDT;

                                    db.Entry(eachcm).State = System.Data.Entity.EntityState.Modified;
                                    //db.Entry(eachcm).CurrentValues.SetValues(eachcm);
                                }
                                db.SaveChanges();
                                CIM_Model cm = db.CIM_Model.Add(new CIM_Model
                                {
                                    StartDT = DateTime.Now,
                                    EndDT = DateTime.ParseExact("01-01-3000", "dd-MM-yyyy", null),
                                    TerminateDT = DateTime.ParseExact("01-01-3000", "dd-MM-yyyy", null),
                                    FileSize = (decimal?)cimModel.FileSizeMB,
                                    FileName = cimModel.FileName,
                                    ObjectCount = cimModel.CountObjectsInModelMap,
                                    ModelCount = cimModel.ModelMap.Count,
                                    //eachcm.Models = string.Join(",", cimModel.ModelMap?.Keys?.ToList());
                                    Models = string.Join(", ", cimModel.ModelMap.Select(x => $"{x.Key}: {x.Value.Count}").ToList()) //cimModel.ToString(),
                                });
                                db.SaveChanges();
                                int modelid = cm?.ID ?? 0;

                                List<CIM_Location> cls = new List<CIM_Location>(); //will be accumulated from all powersystem source types from all devices
                                List<CIM_PositionPoint> cpps = new List<CIM_PositionPoint>(); //will be added once, all location are found from all devices

                                foreach (KeyValuePair<string, SortedDictionary<string, CIMObject>> kk in cimModel.ModelMap)
                                {
                                    string cimelementtype = kk.Key;
                                    string tablename = "CIM_" + cimelementtype;
                                    if (!(pp1.Contains(tablename)))
                                    {
                                        outside.Add(cimelementtype);
                                        continue; //if cim element type is not found in sql server table
                                    }

                                    if (cimelementtype == "Location") continue; //skip location rows, as it is back filled from individual object typs in cim 15 MKM
                                    if (cimelementtype == "PositionPoint") continue; //we will fix the position point once, after finding all locations.

                                    //columns from database
                                    Type m = Type.GetType(mynamespace + "." /*+ "CIMmodel."*/ + tablename);
                                    List<string> mm = m.GetProperties().Where(p => p.CanWrite == true).Select(p => p.Name).ToList();

                                    //if its found try to update the table with values from cimelements
                                    SortedDictionary<string, CIMObject> cimobjectlist = kk.Value;
                                    //Console.WriteLine($"{cimelementtype} is having {cimobjectlist.Count}"); //COUNT OF ITEMS IN EACH OBJECT TYPE
                                    long objectcount = 0;
                                    List<object> entityClassObjs = new List<object>();
                                    foreach (KeyValuePair<string, CIMObject> kkk in cimobjectlist)
                                    {
                                        ++objectcount;
                                        //string cimobjid = kkk.Key;
                                        CIMObject cimobject = kkk.Value;
                                        if (string.IsNullOrEmpty(cimobject?.ID)) continue; //some weired case

                                        if (!(cimobject.HasAttributes)) continue;

                                        List<ObjectAttribute> cimattribs = cimobject.MyAttributesAsList; //cimattribs should be subset of sql server table columns mm
                                        if (objectcount == 1)
                                        {
                                            List<string> itemsextraincimfile = cimattribs.Select(x => x.FullName.Replace('.', '_')).Except(mm).ToList();
                                            if (itemsextraincimfile.Count() > 0) Console.WriteLine($"{cimelementtype} extra in CIMFILE are \n\t" + string.Join("\n\t", itemsextraincimfile));

                                            List<string> itemsextrainsqlcolumns = mm.Except(cimattribs.Select(x => x.FullName.Replace('.', '_')).Union(new List<string> { "ModelID", "ID", "RDF_ID", "IdentifiedObject_exchangeName", "Terminal_1_ID", "Terminal_1_Name", "Terminal_2_ID", "Terminal_2_Name", "ConnectivityNode_A_ID", "ConnectivityNode_A_Name", "ConnectivityNode_B_ID", "ConnectivityNode_B_Name" })).ToList();
                                            if (itemsextrainsqlcolumns.Count() > 0) Console.WriteLine($"{cimelementtype} extra in SQLCOLUMNS are \n\t" + string.Join("\n\t", itemsextrainsqlcolumns));
                                        }

                                        //foreach (ObjectAttribute attr in cimattribs)

                                        //    cimdict[attr.FullName] = attr.ValueOfReference ?? attr.Value;  //if value of refernece not null use value
                                        //    //((Dictionary<string, object>)mydictuniquecols[cimelementtype])[attr.FullName] = attr.ValueOfReference ?? attr.Value; ;
                                        //}

                                        //Try to find winding1 and winding2 in case of powertransformer type
                                        Dictionary<string, string> winding1 = null;
                                        Dictionary<string, string> winding2 = null;
                                        if (cimelementtype == "PowerTransformer") //only case for powertransfomers
                                        {
                                            if (TrafotoWindings != null)
                                            {
                                                if (TrafotoWindings.ContainsKey(cimobject.ID.ToString()))
                                                {
                                                    TrafotoWindings[cimobject.ID.ToString()].TryGetValue("primary", out winding1);
                                                    TrafotoWindings[cimobject.ID.ToString()].TryGetValue("secondary", out winding2);
                                                }
                                            }
                                        }

                                        object entityClassObj = Activator.CreateInstance(m);

                                        /*extra cases to be handled
                                         connectionkind can be added to powertransformerend
                                         ACDCTerminal.sequenceNumber to be added in terminals
                                         in my cim files powertransformerend didn't contained basevoltage
                                         */

                                        foreach (PropertyInfo property in m.GetProperties()) //loop through sql server tablue column names
                                        {
                                            string propname = property.Name; //class field name entityframework (it contains _)
                                            if (propname == "ID") continue; //skip as it is auto handled by database auto increment
                                            string value = "";
                                            if (propname == "ModelID") value = modelid.ToString(); // objectcount.ToString();
                                            else if (propname == "RDF_ID") value = cimobject.ID.ToString();
                                            else if (new List<string> { "PowerTransformerEnd_A_ID", "PowerTransformerEnd_B_ID", "PowerTransformerEnd_A_Name", "PowerTransformerEnd_B_Name" }.Contains(propname) && cimelementtype == "PowerTransformer")
                                            {
                                                if (winding1 != null)
                                                {
                                                    if (propname == "PowerTransformerEnd_A_ID") value = winding1["windid"];
                                                    if (propname == "PowerTransformerEnd_A_Name") value = winding1["windnm"];
                                                }
                                                if (winding2 != null)
                                                {
                                                    if (propname == "PowerTransformerEnd_B_ID") value = winding2["windid"];
                                                    if (propname == "PowerTransformerEnd_B_Name") value = winding2["windnm"];
                                                }
                                            }
                                            //these don't come with cim file object directly, but needed in sql sever cim tables and dgm table for power transformer
                                            else if (new List<string> { "PowerTransformer_HighVoltage", "PowerTransformer_LowVoltage", "PowerTransformer_ratedS" }.Contains(propname) && cimelementtype == "PowerTransformer")
                                            {
                                                if (winding2 != null)
                                                {
                                                    if (propname == "PowerTransformer_ratedS")
                                                    {
                                                        string windind = winding2["windid"]; //can be taken from either side of the the transfomer ends
                                                        CIMObject windobj = cimModel.GetObjectByID(windind);
                                                        if (windobj?.CIMType == "PowerTransformerEnd")
                                                            value = windobj.GetAttributeList("PowerTransformerEnd.ratedS")?.FirstOrDefault()?.Value;
                                                    }
                                                    if (propname == "PowerTransformer_HighVoltage" || propname == "PowerTransformer_LowVoltage")
                                                    {
                                                        string windind = propname == "PowerTransformer_HighVoltage" ? winding1["windid"] : winding2["windid"];
                                                        CIMObject windobj = cimModel.GetObjectByID(windind);
                                                        if (windobj?.CIMType == "PowerTransformerEnd")
                                                            value = windobj.GetAttributeList("BaseVoltage.nominalVoltage")?.FirstOrDefault()?.Value; //may be basevoltage should be found, then nominal voltage, howwever i couldn't find basevoltage.nominalvoltage in transformerend elements in cim files
                                                    }
                                                }
                                            }
                                            else if (new List<string> { "Terminal_1_ID", "Terminal_1_Name", "Terminal_2_ID", "Terminal_2_Name", "ConnectivityNode_A_ID", "ConnectivityNode_A_Name", "ConnectivityNode_B_ID", "ConnectivityNode_B_Name" }.Contains(propname))
                                            {
                                                if (EquipIdtoTermNodes.ContainsKey(cimobject.ID.ToString()))
                                                {
                                                    List<Dictionary<string, string>> termnodedetail = EquipIdtoTermNodes[cimobject.ID.ToString()];
                                                    if (termnodedetail.Count() >= 1) //at least 1 terminal
                                                    {
                                                        if (propname == "Terminal_1_ID") value = termnodedetail.First()["termid"];
                                                        if (propname == "Terminal_1_Name") value = termnodedetail.First()["termnm"];
                                                        if (propname == "ConnectivityNode_A_ID") value = termnodedetail.First()["nodeid"];
                                                        if (propname == "ConnectivityNode_A_Name") value = termnodedetail.First()["nodenm"];
                                                    }
                                                    if (termnodedetail.Count() >= 2) //at least 2 terminals
                                                    {
                                                        if (propname == "Terminal_2_ID") value = termnodedetail.Skip(1).First()["termid"];
                                                        if (propname == "Terminal_2_Name") value = termnodedetail.Skip(1).First()["termnm"];
                                                        if (propname == "ConnectivityNode_B_ID") value = termnodedetail.Skip(1).First()["nodeid"];
                                                        if (propname == "ConnectivityNode_B_Name") value = termnodedetail.Skip(1).First()["nodenm"];
                                                    }
                                                }

                                                if (cimelementtype == "PowerTransformer")
                                                {
                                                    if (winding1 != null)
                                                        if (EquipIdtoTermNodes.ContainsKey(winding1?["windid"]))
                                                        {
                                                            List<Dictionary<string, string>> termnodedetail = EquipIdtoTermNodes[winding1["windid"]];
                                                            if (termnodedetail.Count() >= 1) //at least 1 terminal
                                                            {
                                                                if (propname == "Terminal_1_ID") value = termnodedetail.First()["termid"];
                                                                if (propname == "Terminal_1_Name") value = termnodedetail.First()["termnm"];
                                                                if (propname == "ConnectivityNode_A_ID") value = termnodedetail.First()["nodeid"];
                                                                if (propname == "ConnectivityNode_A_Name") value = termnodedetail.First()["nodenm"];
                                                            }
                                                        }

                                                    if (winding2 != null)
                                                        if (EquipIdtoTermNodes.ContainsKey(winding2?["windid"]))
                                                        {
                                                            List<Dictionary<string, string>> termnodedetail = EquipIdtoTermNodes[winding2["windid"]];
                                                            if (termnodedetail.Count() >= 1) //at least 1 terminal
                                                            {
                                                                if (propname == "Terminal_2_ID") value = termnodedetail.First()["termid"];
                                                                if (propname == "Terminal_2_Name") value = termnodedetail.First()["termnm"];
                                                                if (propname == "ConnectivityNode_B_ID") value = termnodedetail.First()["nodeid"];
                                                                if (propname == "ConnectivityNode_B_Name") value = termnodedetail.First()["nodenm"];
                                                            }
                                                        }
                                                }

                                            }
                                            //else if (propname == "ConductingEquipment_BaseVoltage" && cimelementtype == "ACLineSegment")  //it was taken directly in sql server so not needed to expand the base voltage detail, only pointer detail is enough, so commenting
                                            //{
                                            //    //    cimdict[attr.FullName] = attr.ValueOfReference ?? attr.Value;  //if value of refernece not null use value
                                            //    string acline_Container = cimobject.GetAttributeList("Equipment.MemberOf_EquipmentContainer")?.FirstOrDefault()?.ValueOfReference;
                                            //    CIMObject vLevel = cimModel.GetObjectByID(acline_Container);
                                            //    if (vLevel?.CIMType == "VoltageLevel")
                                            //        value = vLevel.Name; //"13.2KV"
                                            //}

                                            //was not there earlier, added by mkm
                                            else if (propname == "OperationalLimit_type" && cimelementtype == "CurrentLimit") //OperationalLimit_type doesn't exist in cim file
                                            {
                                                string acline_Container = cimobject.GetAttributeList("OperationalLimit.OperationalLimitType")?.FirstOrDefault()?.ValueOfReference;
                                                CIMObject vLevel = cimModel.GetObjectByID(acline_Container);
                                                if (vLevel?.CIMType == "OperationalLimitType")
                                                    value = vLevel.Name;
                                            }
                                            else
                                            {
                                                string cimattribnametomatch = propname;
                                                ObjectAttribute attr = cimattribs.Where(x => x.FullName.Replace('.', '_') == cimattribnametomatch.Replace('.', '_')).FirstOrDefault();
                                                if (attr == null)
                                                {
                                                    //means these names are not found in the cim file or different version of cim, we will try with changing xml element name
                                                    //special case to transfer data to other column
                                                    //for sql server localname column populate the value from cim xml name column
                                                    if (propname == "IdentifiedObject_localName")
                                                        cimattribnametomatch = "IdentifiedObject.name";

                                                    else if (propname == "Switch_ratedCurrent" && cimelementtype == "Breaker")
                                                        cimattribnametomatch = "Breaker.ratedCurrent"; //alternate alias column

                                                    //found in cim file name mismatch
                                                    else if (propname == "Measurement_MemberOf_PSR" && (cimelementtype == "Analog" || cimelementtype == "Discrete"))
                                                        cimattribnametomatch = "Measurement.PowerSystemResource";

                                                    //found in cim file name mismatch
                                                    else if (propname == "AnalogValue_MemberOf_Measurement" && cimelementtype == "AnalogValue")
                                                        cimattribnametomatch = "AnalogValue.Analog";

                                                    //found in cim file name mismatch
                                                    else if (propname == "DiscreteValue_MemberOf_Measurement" && cimelementtype == "DiscreteValue")
                                                        cimattribnametomatch = "DiscreteValue.Discrete";

                                                    //found in cim file name mismatch
                                                    else if (propname == "ConnectivityNode_MemberOf_EquipmentContainer" && cimelementtype == "ConnectivityNode")
                                                        cimattribnametomatch = "ConnectivityNode.ConnectivityNodeContainer";

                                                    ////found in cim file name mismatch
                                                    //if (propname == "Equipment_MemberOf_EquipmentContainer") // && (cimelementtype == "PowerTransformer" || cimelementtype == "BusbarSection"))
                                                    //    cimattribnametomatch = "Equipment.EquipmentContainer";
                                                    //
                                                    ////found in cim file name mismatch
                                                    //if (propname == "VoltageLevel_MemberOf_Substation" && cimelementtype == "VoltageLevel")
                                                    //    cimattribnametomatch = "VoltageLevel.Substation";
                                                    //
                                                    ////found in cim 15
                                                    //if (propname == "TransformerWinding_MemberOf_PowerTransformer" && cimelementtype == "TransformerWinding")
                                                    //    cimattribnametomatch = "TransformerWinding.PowerTransformer";

                                                    //found mostly the _MemberOf_ is removed in cim15
                                                    else if (propname.Contains("_MemberOf_")) cimattribnametomatch = propname.Replace("_MemberOf_", ".");

                                                    //found in cim15 
                                                    else if (propname.Contains("Conductor") && cimelementtype == "ACLineSegment") //conductor.r conductor.x conductor.x0 conductor_r0
                                                        cimattribnametomatch = propname.Replace("Conductor", "ACLineSegment");

                                                    //try again finding
                                                    attr = cimattribs.Where(x => x.FullName.Replace('.', '_') == cimattribnametomatch.Replace('.', '_')).FirstOrDefault();
                                                }

                                                if (attr != null)
                                                {
                                                    value = attr.ValueOfReference ?? attr.Value;
                                                    //special case where we need ABC instead of http://iec.ch/TC57/2006/CIM-schema-cim12#Phases.ABC
                                                    //if (cimattribnametomatch.EndsWith("_phases")) //if columnname like ConductingEquipment_phases
                                                    //    value = value.Split('#').LastOrDefault().Split('.').LastOrDefault();
                                                }
                                                else
                                                {
                                                    //IF PROPERTY TYPE IS STRING , SET N/A FOR DEBUG purpose only
                                                    if (property.PropertyType == typeof(string))
                                                    {
                                                        if (showhypenforna)
                                                            value = "---"; //can be set to null also
                                                    }
                                                    else
                                                    {
                                                        //if found int or double, also, all are nullable types which columns are not found
                                                    }
                                                }
                                            }

                                            object tosetvalue = ConvertValue(property.PropertyType, value);
                                            try
                                            {
                                                property.SetValue(entityClassObj, tosetvalue, null);
                                            }
                                            catch (Exception ex6)
                                            {
                                            }

                                            /*
                                             final steps was in stored procedure , which were not coming directly from cim, but in c# we can do it directly
                                             powertransformerend -> set basevoltage_nominalvoltage, find base voltage, read nominal and then set in the column

                                            --Procedure to set the ParentName of CIM tables
                                            EXECUTE [dbo].[PR_CIM_SetParentName]
                                            set parent, i understood for powertransformer itis the substation, member of equipment container, 
                                            and for other devices, it could be the powertransfomer id and name, although it is much complex to understand from the stored procedure

                                            --Procedure to import data from CIM tables to WS_GEO_DataPoint
                                            EXECUTE PR_CIM_WebGeo_Import
                                             */
                                        }//ONE ROW FOR ANY DEVICE TYPE is FINISHED HERE

                                        /*--Procedure to set the ObjectRDF_ID and ObjectType columns in CIM_PositionPoint
                                            EXECUTE [dbo].[PR_CIM_UpdatePositionPoint]*/
                                        /*add to PostionPoint table FOR specific 16 table types*/
                                        ////if(Enum.IsDefined(typeof(TypesForPositionPoint), tablename))
                                        //if (Enum.TryParse(tablename, out TypesForPositionPoint ObjectType))
                                        //{
                                        //    //SET PosistionPoint_Object_RDF_ID = RDF_ID,
                                        //    //PositionPoint_ObjectType = ObjectType
                                        //    //CIM_Location.Location_PowerSystemResource == switch.RDF_ID
                                        //    //CIM_Location.RDF_ID = CIM_PositionPoint.PositionPoint_Location
                                        //}

                                        //found in CIM 15, inside device element node, there is a pointer to location, and from poistion point(containing lat long) similar pointer to location found
                                        string locationid = cimobject.GetAttributeList("PowerSystemResource.Location")?.FirstOrDefault()?.ValueOfReference;
                                        if (!string.IsNullOrEmpty(locationid))
                                        {
                                            CIM_Location cl = new CIM_Location
                                            {
                                                ModelID = modelid,
                                                RDF_ID = locationid,
                                                Location_PowerSystemResource = cimobject.ID
                                            };
                                            cls.Add(cl);
                                        }

                                        //one row added for any table
                                        entityClassObjs.Add(entityClassObj);
                                    }//ONE DEVICE TYPE, ALL ROWS FINISHED IN LOOP

                                    /*my method of bulk insert*/
                                    #region MyBulkInsert
                                    if (tablename == "CIM_ACLineSegment") db.BulkInsertAll(entityClassObjs.Cast<CIM_ACLineSegment>());
                                    if (tablename == "CIM_Analog") db.BulkInsertAll(entityClassObjs.Cast<CIM_Analog>());
                                    if (tablename == "CIM_AnalogValue") db.BulkInsertAll(entityClassObjs.Cast<CIM_AnalogValue>());
                                    if (tablename == "CIM_ApparentPowerLimit") db.BulkInsertAll(entityClassObjs.Cast<CIM_ApparentPowerLimit>());
                                    if (tablename == "CIM_BaseVoltage") db.BulkInsertAll(entityClassObjs.Cast<CIM_BaseVoltage>());
                                    if (tablename == "CIM_Breaker") db.BulkInsertAll(entityClassObjs.Cast<CIM_Breaker>());
                                    if (tablename == "CIM_BusbarSection") db.BulkInsertAll(entityClassObjs.Cast<CIM_BusbarSection>());
                                    if (tablename == "CIM_Command") db.BulkInsertAll(entityClassObjs.Cast<CIM_Command>());
                                    if (tablename == "CIM_ConformLoad") db.BulkInsertAll(entityClassObjs.Cast<CIM_ConformLoad>());
                                    if (tablename == "CIM_ConnectivityNode") db.BulkInsertAll(entityClassObjs.Cast<CIM_ConnectivityNode>());
                                    if (tablename == "CIM_CurrentLimit") db.BulkInsertAll(entityClassObjs.Cast<CIM_CurrentLimit>());
                                    if (tablename == "CIM_Disconnector") db.BulkInsertAll(entityClassObjs.Cast<CIM_Disconnector>());
                                    if (tablename == "CIM_Discrete") db.BulkInsertAll(entityClassObjs.Cast<CIM_Discrete>());
                                    if (tablename == "CIM_DiscreteValue") db.BulkInsertAll(entityClassObjs.Cast<CIM_DiscreteValue>());
                                    if (tablename == "CIM_EnergyConsumer") db.BulkInsertAll(entityClassObjs.Cast<CIM_EnergyConsumer>());
                                    if (tablename == "CIM_Feeder") db.BulkInsertAll(entityClassObjs.Cast<CIM_Feeder>());
                                    if (tablename == "CIM_Fuse") db.BulkInsertAll(entityClassObjs.Cast<CIM_Fuse>());
                                    if (tablename == "CIM_GeneratingUnit") db.BulkInsertAll(entityClassObjs.Cast<CIM_GeneratingUnit>());
                                    if (tablename == "CIM_Ground") db.BulkInsertAll(entityClassObjs.Cast<CIM_Ground>());
                                    if (tablename == "CIM_GroundDisconnector") db.BulkInsertAll(entityClassObjs.Cast<CIM_GroundDisconnector>());
                                    if (tablename == "CIM_Line") db.BulkInsertAll(entityClassObjs.Cast<CIM_Line>());
                                    if (tablename == "CIM_LoadBreakSwitch") db.BulkInsertAll(entityClassObjs.Cast<CIM_LoadBreakSwitch>());
                                    //if (tablename == "CIM_Location") db.BulkInsertAll(entityClassObjs.Cast<CIM_Location>());
                                    //if (tablename == "CIM_Model") db.BulkInsertAll(entityClassObjs.Cast<CIM_Model>());
                                    //if (tablename == "CIM_PositionPoint") db.BulkInsertAll(entityClassObjs.Cast<CIM_PositionPoint>());
                                    if (tablename == "CIM_PositionPointFormat") db.BulkInsertAll(entityClassObjs.Cast<CIM_PositionPointFormat>());
                                    if (tablename == "CIM_PowerTransformer") db.BulkInsertAll(entityClassObjs.Cast<CIM_PowerTransformer>());
                                    if (tablename == "CIM_PowerTransformerEnd") db.BulkInsertAll(entityClassObjs.Cast<CIM_PowerTransformerEnd>());
                                    if (tablename == "CIM_SetPoint") db.BulkInsertAll(entityClassObjs.Cast<CIM_SetPoint>());
                                    if (tablename == "CIM_ShuntCompensator") db.BulkInsertAll(entityClassObjs.Cast<CIM_ShuntCompensator>());
                                    if (tablename == "CIM_Substation") db.BulkInsertAll(entityClassObjs.Cast<CIM_Substation>());
                                    if (tablename == "CIM_Switch") db.BulkInsertAll(entityClassObjs.Cast<CIM_Switch>());
                                    if (tablename == "CIM_SynchronousMachine") db.BulkInsertAll(entityClassObjs.Cast<CIM_SynchronousMachine>());
                                    if (tablename == "CIM_TapChanger") db.BulkInsertAll(entityClassObjs.Cast<CIM_TapChanger>());
                                    if (tablename == "CIM_Terminal") db.BulkInsertAll(entityClassObjs.Cast<CIM_Terminal>());
                                    if (tablename == "CIM_TransformerWinding") db.BulkInsertAll(entityClassObjs.Cast<CIM_TransformerWinding>());
                                    if (tablename == "CIM_VoltageLevel") db.BulkInsertAll(entityClassObjs.Cast<CIM_VoltageLevel>());
                                    if (tablename == "CIM_EquivalentInjection") db.BulkInsertAll(entityClassObjs.Cast<CIM_EquivalentInjection>());
                                    #endregion
                                    //db.SaveChangesAsync();

                                    #region DGM
                                    /* DGM_BaseSwitch TABLE INSERT */
                                    /*--Procedure to import data from CIM tables to DGM tables
                                       EXECUTE PR_CIM_ToGISImport*/
                                    if (tablename == "CIM_Breaker" ||
                                        tablename == "CIM_Disconnector" ||
                                        tablename == "CIM_Fuse" ||
                                        tablename == "CIM_LoadBreakSwitch" ||
                                        tablename == "CIM_Switch")
                                    {
                                        List<DGM_BaseSwitch> dbs = new List<DGM_BaseSwitch>();
                                        dynamic ecos = null;
                                        if (tablename == "CIM_Breaker") ecos = entityClassObjs.Cast<CIM_Breaker>();
                                        if (tablename == "CIM_Disconnector") ecos = entityClassObjs.Cast<CIM_Disconnector>();
                                        if (tablename == "CIM_Fuse") ecos = entityClassObjs.Cast<CIM_Fuse>();
                                        if (tablename == "CIM_LoadBreakSwitch") ecos = entityClassObjs.Cast<CIM_LoadBreakSwitch>();
                                        if (tablename == "CIM_Switch") ecos = entityClassObjs.Cast<CIM_Switch>();
                                        foreach (dynamic cb in ecos)
                                        {
                                            DGM_BaseSwitch db1 = new DGM_BaseSwitch
                                            {
                                                ID = 0, //Default Id to 0. Since five CIM tables are inserted into DGM_BaseSwitch, Id's are set after Inserting
                                                Name = cb.IdentifiedObject_name,
                                                SwitchTypeID = 0, //SwitchType of Circuit Breaker. Found in DGM_SwitchType
                                                PhaseTypeID = 7, //PhaseType of ABC. Found in DGM_PhaseType. Phase is undefined in CIM XML. Default to ABC
                                                NormalStatus = null, //(byte)((cb.Switch_normalOpen == "true") ? 0: 1;
                                                ANodeName = cb.ConnectivityNode_A_Name,
                                                BNodeName = cb.ConnectivityNode_B_Name,
                                                StartDT = cm.StartDT,
                                                EndDT = cm.EndDT,
                                                CreateDT = cm.CreateDT,
                                                TerminateDT = cm.TerminateDT
                                            };
                                            if (cb.Switch_normalOpen == "true") db1.NormalStatus = 0;
                                            if (cb.Switch_normalOpen == "false") db1.NormalStatus = 1;
                                            if (Enum.TryParse(tablename, out DGM_SwitchType dst))
                                                db1.SwitchTypeID = (int)dst;
                                            //if (tablename == "CIM_Breaker") db1.SwitchTypeID = 1;
                                            dbs.Add(db1);
                                        }
                                        db.BulkInsertAll<DGM_BaseSwitch>(dbs); //write all to dgm base switch
                                    }//DGM_BaseSwitch table insert finished

                                    /* insert into DGM_BaseTerminalToConnectivityNode*/
                                    if (tablename == "CIM_Terminal")
                                    {
                                        ////one time search for all nodes detail from the complete cim file
                                        //var connnodeid2name = new Dictionary<string, string>();
                                        //var allnodes = cimModel.GetAllObjectsOfType(CIMConstants.TypeNameConnectivityNode);
                                        //if (allnodes != null)
                                        //    foreach (var node in allnodes.Values)
                                        //        connnodeid2name[node.ID] = node.Name;

                                        List<DGM_BaseTerminalToConnectivityNode> dbs = new List<DGM_BaseTerminalToConnectivityNode>();
                                        IEnumerable<CIM_Terminal> dbttcs = entityClassObjs.Cast<CIM_Terminal>();
                                        foreach (CIM_Terminal dbttc in dbttcs)
                                        {
                                            string connnodeid = dbttc.Terminal_ConnectivityNode;
                                            //connnodeid2name.TryGetValue(connnodeid, out string connnodename); //if we find all node detail one time use this
                                            //we can also do find each node type by cimModel.GetObjectByID(connnodeid);
                                            string connnodename = cimModel.GetObjectByID(connnodeid)?.Name ?? "";
                                            DGM_BaseTerminalToConnectivityNode dbss = new DGM_BaseTerminalToConnectivityNode
                                            {
                                                TerminalName = dbttc.IdentifiedObject_name,
                                                ConnectivityNodeName = connnodename
                                            };
                                            dbs.Add(dbss);
                                        }
                                        db.BulkInsertAll<DGM_BaseTerminalToConnectivityNode>(dbs);
                                    }
                                    /*DGM_BaseTerminalToConnectivityNode table insert finished*/

                                    /*insert into DGM_BaseLine and DGM_LineElectricalParameter */
                                    if (tablename == "CIM_ACLineSegment")
                                    {
                                        List<DGM_BaseLine> dbls = new List<DGM_BaseLine>();
                                        List<DGM_LineElectricalParameter> dleps = new List<DGM_LineElectricalParameter>();
                                        IEnumerable<CIM_ACLineSegment> dbaclines = entityClassObjs.Cast<CIM_ACLineSegment>(); //can also be read from db.cim_aclinesegment
                                        foreach (CIM_ACLineSegment dbacline in dbaclines)
                                        {
                                            DGM_BaseLine dbl = new DGM_BaseLine
                                            {
                                                ID = dbacline.ID,
                                                //Name = dbacline.IdentifiedObject_name,
                                                ANodeName = dbacline.ConnectivityNode_A_Name,
                                                BNodeName = dbacline.ConnectivityNode_B_Name,
                                                PhaseTypeID = 7, //--PhaseType of ABC.Found in DGM_PhaseType
                                                StartDT = cm.StartDT,
                                                EndDT = cm.EndDT,
                                                CreateDT = cm.CreateDT,
                                                TerminateDT = cm.TerminateDT
                                            };
                                            dbls.Add(dbl);
                                        }
                                        db.BulkInsertAll<DGM_BaseLine>(dbls);

                                        //since indexid is generated by database, we need to read it before storing it in lineelectricalparameter table
                                        Dictionary<int, int> lineidtobaselineindex = new Dictionary<int, int>();
                                        foreach (DGM_BaseLine eachdbl in db.DGM_BaseLine)
                                            lineidtobaselineindex[eachdbl.ID] = eachdbl.IndexID;
                                        foreach (CIM_ACLineSegment dbacline in dbaclines)
                                        {
                                            lineidtobaselineindex.TryGetValue(dbacline.ID, out int baselineindex);
                                            DGM_LineElectricalParameter dlep = new DGM_LineElectricalParameter
                                            {
                                                LineID = baselineindex,
                                                raa = (decimal?)dbacline.Conductor_r,
                                                rbb = (decimal?)dbacline.Conductor_r,
                                                rcc = (decimal?)dbacline.Conductor_r,
                                                xaa = (decimal?)dbacline.Conductor_x,
                                                xbb = (decimal?)dbacline.Conductor_x,
                                                xcc = (decimal?)dbacline.Conductor_x,
                                                Baa = (decimal?)dbacline.Conductor_bch,
                                                Bbb = (decimal?)dbacline.Conductor_bch,
                                                Bcc = (decimal?)dbacline.Conductor_bch,
                                            };
                                            dleps.Add(dlep);
                                        }
                                        db.BulkInsertAll<DGM_LineElectricalParameter>(dleps);
                                    }
                                    /*insert into DGM_BaseLine and DGM_LineElectricalParameter finished */

                                    /* insert into DGM_BaseTransformer started*/
                                    if (tablename == "CIM_PowerTransformer")
                                    {
                                        List<DGM_BaseTransformer> dbts = new List<DGM_BaseTransformer>();
                                        IEnumerable<CIM_PowerTransformer> cptsobjs = entityClassObjs.Cast<CIM_PowerTransformer>(); //can also be read from db.cim_powertransformer
                                        foreach (CIM_PowerTransformer cpt in cptsobjs)
                                        {
                                            string hvsideid = cpt.PowerTransformerEnd_A_ID;
                                            string lvsideid = cpt.PowerTransformerEnd_B_ID;

                                            DGM_BaseTransformer dbt = new DGM_BaseTransformer
                                            {
                                                ID = cpt.ID,
                                                Name = cpt.IdentifiedObject_name,
                                                PrimaryNodeName = cpt.ConnectivityNode_A_Name,
                                                SecondaryNodeName = cpt.ConnectivityNode_B_Name,
                                                PrimaryPhaseTypeID = 7, //--PhaseType of ABC. Found in DGM_PhaseType
                                                SecondaryPhaseTypeID = 7, //--PhaseType of ABC. Found in DGM_PhaseType
                                                StartDT = cm.StartDT,
                                                EndDT = cm.EndDT,
                                                CreateDT = cm.CreateDT,
                                                TerminateDT = cm.TerminateDT,
                                                RatingKVA = (decimal?)cpt.PowerTransformer_ratedS, //as i have found the rateds, high, low earlier and filled in cim file, that value can be used
                                                VoltNomHigh = (decimal?)cpt.PowerTransformer_HighVoltage, //if nominal basevoltage not found in transfomrerend, these values are coming null
                                                VoltNomLow = (decimal?)cpt.PowerTransformer_LowVoltage
                                            };
                                            dbts.Add(dbt);
                                        }
                                        db.BulkInsertAll<DGM_BaseTransformer>(dbts);
                                    }
                                    /* DGM_BaseTransformer insert finished*/
                                    /* insert into DGM_BaseServicePoint */
                                    /*
                                        INSERT INTO DGM_BaseServicePoint (
	                                        ID,
	                                        Name,
	                                        TransformerID,
	                                        ConnectivityNodeName
                                        )
                                        SELECT
	                                        CIM_EnergyConsumer.ID,
	                                        CIM_EnergyConsumer.IdentifiedObject_name,
	                                        CIM_EnergyConsumer.PowerTransformer_ID,
	                                        CIM_EnergyConsumer.ConnectivityNode_Name
                                        FROM
	                                        CIM_EnergyConsumer
                                        WHERE CIM_EnergyConsumer.ModelID = @ModelID
                                     */
                                    if (tablename == "CIM_EnergyConsumer")
                                    {
                                        List<DGM_BaseServicePoint> dbsps = new List<DGM_BaseServicePoint>();
                                        IEnumerable<CIM_EnergyConsumer> cecs = entityClassObjs.Cast<CIM_EnergyConsumer>();
                                        foreach (CIM_EnergyConsumer cec in cecs)
                                        {
                                            DGM_BaseServicePoint dbp = new DGM_BaseServicePoint
                                            {
                                                ID = cec.ID,
                                                Name = cec.IdentifiedObject_name,
                                                TransformerID = cec.PowerTransformer_ID,
                                                ConnectivityNodeName = cec.ConnectivityNode_Name
                                            };
                                            dbsps.Add(dbp);
                                        }
                                        db.BulkInsertAll<DGM_BaseServicePoint>(dbsps);
                                    }
                                    /* DGM_BaseServicePoint insert finished */

                                    /*DGM_BaseConnectivityNode insert start*/
                                    /*
                                        INSERT INTO DGM_BaseConnectivityNode (
	                                        Name,
	                                        NodeTypeID
                                        )
                                        SELECT
	                                        CIM_ConnectivityNode.IdentifiedObject_name,
	                                        4 --regular node. NodeType is undefined in CIM XML. Default to regular node
                                        FROM
	                                        CIM_ConnectivityNode
                                        WHERE
	                                        CIM_ConnectivityNode.ModelID = @ModelID
                                     */
                                    if (tablename == "CIM_ConnectivityNode")
                                    {
                                        List<DGM_BaseConnectivityNode> dbsps = new List<DGM_BaseConnectivityNode>();
                                        IEnumerable<CIM_ConnectivityNode> cecs = entityClassObjs.Cast<CIM_ConnectivityNode>();
                                        foreach (CIM_ConnectivityNode cec in cecs)
                                        {
                                            DGM_BaseConnectivityNode dbp = new DGM_BaseConnectivityNode
                                            {
                                                Name = cec.IdentifiedObject_name,
                                                NodeTypeID = 4 // --regular node. NodeType is undefined in CIM XML. Default to regular node
                                            };
                                            dbsps.Add(dbp);
                                        }
                                        db.BulkInsertAll<DGM_BaseConnectivityNode>(dbsps);
                                    }
                                    /*DGM_BaseConnectivityNode insert finished*/

                                    /*DGM_BaseSubstation insert started */
                                    if (tablename == "CIM_Substation")
                                    {
                                        //find all voltagelevels, there is a condition if the station has below 30 only exclude those stations
                                        //var connnodeid2name = new Dictionary<string, string>();
                                        Dictionary<string, List<double>> stationhighvolts = new Dictionary<string, List<double>>();
                                        SortedDictionary<string, CIMObject> allvoltlevels = cimModel.GetAllObjectsOfType(CIMConstants.TypeNameVoltageLevel);
                                        if (allvoltlevels != null)
                                            foreach (CIMObject volt in allvoltlevels.Values)
                                            {
                                                string stationid = volt.GetAttributeList("VoltageLevel.MemberOf_Substation")?.FirstOrDefault()?.Value;
                                                if (stationid == null)
                                                    stationid = volt.GetAttributeList("VoltageLevel.Substation")?.FirstOrDefault()?.ValueOfReference;
                                                string highvoltstr = volt.GetAttributeList("VoltageLevel.highVoltageLimit")?.FirstOrDefault()?.Value;
                                                double.TryParse(highvoltstr ?? "", out double highvolt);
                                                if (stationid != null)
                                                {
                                                    if (!stationhighvolts.ContainsKey(stationid))
                                                        stationhighvolts[stationid] = new List<double>();
                                                    if (highvolt > 0)
                                                        stationhighvolts[stationid].Add(highvolt);
                                                }
                                            }

                                        List<DGM_BaseSubstation> dbsps = new List<DGM_BaseSubstation>();
                                        IEnumerable<CIM_Substation> cecs = entityClassObjs.Cast<CIM_Substation>();
                                        foreach (CIM_Substation cec in cecs)
                                        {
                                            if (stationhighvolts.ContainsKey(cec.RDF_ID))
                                                if (stationhighvolts[cec.RDF_ID].Count > 0)
                                                    if (stationhighvolts[cec.RDF_ID].All(x => x < 30))
                                                        continue; //if all the voltages are less than 30 kv then skip that station

                                            DGM_BaseSubstation dbp = new DGM_BaseSubstation
                                            {
                                                ID = cec.ID,
                                                Name = cec.IdentifiedObject_name,
                                            };
                                            dbsps.Add(dbp);
                                        }
                                        db.BulkInsertAll<DGM_BaseSubstation>(dbsps);
                                    }
                                    /*DGM_BaseSubstation insert finished */

                                    /*DGM_BaseBusbarSection insert started*/
                                    /*
                                        INSERT INTO DGM_BaseBusbarSection
                                        SELECT
	                                        CIM_EquivalentInjection.IdentifiedObject_name,
	                                        CIM_EquivalentInjection.ConnectivityNode_Name
                                        FROM
	                                        CIM_EquivalentInjection
                                     */
                                    if (tablename == "CIM_EquivalentInjection")
                                    {
                                        List<DGM_BaseBusbarSection> dbsps = new List<DGM_BaseBusbarSection>();
                                        IEnumerable<CIM_EquivalentInjection> cecs = entityClassObjs.Cast<CIM_EquivalentInjection>();
                                        foreach (CIM_EquivalentInjection cec in cecs)
                                        {
                                            DGM_BaseBusbarSection dbp = new DGM_BaseBusbarSection
                                            {
                                                Name = cec.IdentifiedObject_name,
                                                NodeName = cec.ConnectivityNode_Name
                                            };
                                            dbsps.Add(dbp);
                                        }
                                        db.BulkInsertAll<DGM_BaseBusbarSection>(dbsps);
                                    }
                                    /*DGM_BaseBusbarSection insert finished*/
                                    #endregion DGM

                                }//ALL DEVICE TYPES FINISHED
                                db.SaveChanges();

                                //update the location table
                                if (cls.Count > 0)
                                    db.BulkInsertAll(cls); //write all location at once

                                #region PositionPointFill
                                //update the position points table
                                SortedDictionary<string, CIMObject> allpositionpoints = cimModel.GetAllObjectsOfType("PositionPoint");
                                if (allpositionpoints != null)
                                {
                                    foreach (CIMObject pospnt in allpositionpoints.Values)
                                    {
                                        int objtype = -1;
                                        string pseid = null;
                                        string posid = pospnt.ID;
                                        double.TryParse(pospnt.GetAttributeList("PositionPoint.xPosition")?.FirstOrDefault().Value ?? "", out double xpos);
                                        double.TryParse(pospnt.GetAttributeList("PositionPoint.yPosition")?.FirstOrDefault().Value ?? "", out double ypos);
                                        double.TryParse(pospnt.GetAttributeList("PositionPoint.zPosition")?.FirstOrDefault().Value ?? "", out double zpos);
                                        string loca = pospnt.GetAttributeList("PositionPoint.Location")?.FirstOrDefault().ValueOfReference;
                                        CIM_Location locationobj = cls.Where(x => x.RDF_ID == loca)?.FirstOrDefault();
                                        if (locationobj != null)
                                        {
                                            pseid = locationobj.Location_PowerSystemResource;
                                            string pseobjtype = cimModel.GetObjectByID(pseid)?.CIMType;

                                            if (Enum.TryParse(pseobjtype, out TypesForPositionPoint tfpp))
                                                objtype = (int)tfpp;
                                        }
                                        CIM_PositionPoint cpp = new CIM_PositionPoint
                                        {
                                            ModelID = modelid,
                                            RDF_ID = pospnt.ID,
                                            IdentifiedObject_name = pospnt.GetAttributeList("IdentifiedObject.name")?.FirstOrDefault().Value,
                                            IdentifiedObject_exchangeName = pospnt.GetAttributeList("IdentifiedObject.exchangeName")?.FirstOrDefault().Value,
                                            IdentifiedObject_pathName = pospnt.GetAttributeList("IdentifiedObject.pathName")?.FirstOrDefault().Value,
                                            IdentifiedObject_mRID = pospnt.GetAttributeList("IdentifiedObject.mRID")?.FirstOrDefault().Value,
                                            IdentifiedObject_localName = pospnt.GetAttributeList("IdentifiedObject.localName")?.FirstOrDefault().Value,
                                            IdentifiedObject_description = pospnt.GetAttributeList("IdentifiedObject.description")?.FirstOrDefault().Value,
                                            IdentifiedObject_aliasName = pospnt.GetAttributeList("IdentifiedObject.aliasName")?.FirstOrDefault().Value,
                                            PositionPoint_Location = loca,
                                            PositionPoint_yPosition = ypos,
                                            PositionPoint_xPosition = xpos,
                                            //PositionPoint_sequenceNumber = "",
                                            PosistionPoint_Object_RDF_ID = pseid,
                                            PositionPoint_ObjectType = objtype
                                        };
                                        cpps.Add(cpp);
                                    }

                                }
                                if (cpps.Count > 0)
                                    db.BulkInsertAll(cpps); //write all location at once
                                #endregion PositionPointFill

                                //*setting id = indexid for the dgm_baseswitch AS id was set to 0 , and inserted from various swtich tables.*/
                                db.Database.ExecuteSqlCommand("update DGM_BaseSwitch set ID = IndexID");

                            }
                            catch (Exception ex5)
                            {
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine(ex5.Message);
                                Console.WriteLine(ex5.StackTrace);
                                Console.ResetColor();
                                Console.Read();
                            }

                            #region notfoundindb
                            /*
                             * LOADBREAKERSWITCH TERMINAL AND NODE ID NEEDS TO BE VARCHAR INSTEAD OF INT
                             * 
                                Accumulator
                                AccumulatorValue
                                Company
                                ConnectivityNodeContainer
                                NonConformLoad
                                RecloseSequence
                                not found in sql server for the file \MidtownCimRdf.xml

                                Accumulator
                                AccumulatorValue
                                Company
                                ConnectivityNodeContainer
                                RecloseSequence
                                not found in sql server for the file \kcpl_midtown.xml

                                EquivalentNetwork
                                GeographicalRegion
                                MeasurementValueSource
                                Name
                                NameType
                                OperationalLimitSet
                                OperationalLimitType
                                SubGeographicalRegion
                                not found in sql server for the file \CIM_417740.xml

                                Diagram
                                DiagramObject
                                DiagramPoint
                                not found in sql server for the file \CIM_417740_PowerOn132.xml

                                CoordinateSystem
                                not found in sql server for the file \CIM_417740_27700.xml
                            */
                            #endregion
                            #region extratablesindb
                            /*
                            ACLineSegment extra in sql columns are
                                    
                                    ConductingEquipment_BaseVoltage
                                    Terminal_1_ID
                                    Terminal_1_Name
                                    Terminal_2_ID
                                    Terminal_2_Name
                                    ConnectivityNode_A_ID
                                    ConnectivityNode_A_Name
                                    ConnectivityNode_B_ID
                                    ConnectivityNode_B_Name
                                    ParentName
                            Analog extra in sql columns are
                                    
                            AnalogValue extra in sql columns are
                                    
                                    MeasurementValue_MeasurementValueSource
                            Breaker extra in sql columns are
                                    Switch_ratedCurrent
                                    
                                    Switch_retained
                                    Terminal_1_ID
                                    Terminal_1_Name
                                    Terminal_2_ID
                                    Terminal_2_Name
                                    ConnectivityNode_A_ID
                                    ConnectivityNode_A_Name
                                    ConnectivityNode_B_ID
                                    ConnectivityNode_B_Name
                                    ParentName
                            BusbarSection extra in sql columns are
                                    
                            Command extra in sql columns are
                                    
                            ConformLoad extra in sql columns are
                                    
                            ConnectivityNode extra in sql columns are
                                    
                            Disconnector extra in sql columns are
                                    Switch_retained
                                    Terminal_1_ID
                                    Terminal_1_Name
                                    Terminal_2_ID
                                    Terminal_2_Name
                                    ConnectivityNode_A_ID
                                    ConnectivityNode_A_Name
                                    ConnectivityNode_B_ID
                                    ConnectivityNode_B_Name
                                    ParentName
                            Discrete extra in sql columns are
                                    
                                    Measurement_measurementType
                            DiscreteValue extra in sql columns are
                                    
                                    MeasurementValue_MeasurementValueSource
                            Feeder extra in sql columns are
                                    
                            Fuse extra in sql columns are
                                    Terminal_1_ID
                                    Terminal_1_Name
                                    Terminal_2_ID
                                    Terminal_2_Name
                                    ConnectivityNode_A_ID
                                    ConnectivityNode_A_Name
                                    ConnectivityNode_B_ID
                                    ConnectivityNode_B_Name
                            LoadBreakSwitch extra in sql columns are
                                    Switch_retained
                                    Terminal_1_Name
                                    Terminal_1_ID
                                    Terminal_2_Name
                                    Terminal_2_ID
                                    ConnectivityNode_A_Name
                                    ConnectivityNode_A_ID
                                    ConnectivityNode_B_Name
                                    ConnectivityNode_B_ID
                                    ParentName
                            PowerTransformer extra in sql columns are
                                    
                                    Terminal_1_ID
                                    Terminal_1_Name
                                    Terminal_2_ID
                                    Terminal_2_Name
                                    ConnectivityNode_A_ID
                                    ConnectivityNode_A_Name
                                    ConnectivityNode_B_ID
                                    ConnectivityNode_B_Name
                                    PowerTransformerEnd_A_ID
                                    PowerTransformerEnd_A_Name
                                    PowerTransformerEnd_B_ID
                                    PowerTransformerEnd_B_Name
                                    PowerTransformer_ratedS
                                    PowerTransformer_HighVoltage
                                    PowerTransformer_LowVoltage
                                    ParentName
                            SetPoint extra in sql columns are
                                    
                            ShuntCompensator extra in sql columns are
                                    
                            Substation extra in sql columns are
                                    
                                    Substation_Region
                            TapChanger extra in sql columns are
                                    
                                    TapChanger_RegulationSchedule
                                    TapChanger_Terminal
                            Terminal extra in sql columns are
                                    
                            TransformerWinding extra in sql columns are
                                    
                            VoltageLevel extra in sql columns are
                                    
                                    VoltageLevel_MemberOf_Substation
                             */
                            #endregion
                            #region extracolumnsindb
                            /*
                                ACLineSegment extra in sql columns are
                                        ConductingEquipment_BaseVoltage
                                        ParentName
                                AnalogValue extra in sql columns are
                                        MeasurementValue_MeasurementValueSource
                                Breaker extra in sql columns are
                                        Switch_ratedCurrent
                                        Switch_retained
                                        ParentName
                                Disconnector extra in sql columns are
                                        Switch_retained
                                        ParentName
                                Discrete extra in sql columns are
                                        Measurement_measurementType
                                DiscreteValue extra in sql columns are
                                        MeasurementValue_MeasurementValueSource
                                LoadBreakSwitch extra in sql columns are
                                        Switch_retained
                                        ParentName
                                PowerTransformer extra in sql columns are
                                        PowerTransformerEnd_A_ID
                                        PowerTransformerEnd_A_Name
                                        PowerTransformerEnd_B_ID
                                        PowerTransformerEnd_B_Name
                                        PowerTransformer_ratedS
                                        PowerTransformer_HighVoltage
                                        PowerTransformer_LowVoltage
                                        ParentName
                                Substation extra in sql columns are
                                        Substation_Region
                                TapChanger extra in sql columns are
                                        TapChanger_RegulationSchedule
                                        TapChanger_Terminal
                                VoltageLevel extra in sql columns are
                                        VoltageLevel_MemberOf_Substation

                             */
                            #endregion
                            #region differencesincimanddb
                            /*
                                ACLineSegment extra in SQLCOLUMNS are
                                        ConductingEquipment_BaseVoltage
                                AnalogValue extra in SQLCOLUMNS are
                                        MeasurementValue_MeasurementValueSource
                                Breaker extra in CIMFILE are
                                        Breaker_ratedCurrent
                                Breaker extra in SQLCOLUMNS are
                                        Switch_ratedCurrent
                                        Switch_retained
                                ConformLoad extra in CIMFILE are
                                        EnergyConsumer_qFexp
                                Disconnector extra in SQLCOLUMNS are
                                        Switch_retained
                                Discrete extra in SQLCOLUMNS are
                                        Measurement_measurementType
                                DiscreteValue extra in SQLCOLUMNS are
                                        MeasurementValue_MeasurementValueSource
                                LoadBreakSwitch extra in SQLCOLUMNS are
                                        Switch_retained
                                PowerTransformer extra in SQLCOLUMNS are
                                        PowerTransformerEnd_A_ID
                                        PowerTransformerEnd_A_Name
                                        PowerTransformerEnd_B_ID
                                        PowerTransformerEnd_B_Name
                                        PowerTransformer_ratedS
                                        PowerTransformer_HighVoltage
                                        PowerTransformer_LowVoltage
                                ShuntCompensator extra in CIMFILE are
                                        RegulatingCondEq_RegulationSchedule
                                Substation extra in SQLCOLUMNS are
                                        Substation_Region
                                TapChanger extra in CIMFILE are
                                        TapChanger_stepPhaseShiftIncrement
                                        TapChanger_neutralU
                                TapChanger extra in SQLCOLUMNS are
                                        TapChanger_Terminal
                                VoltageLevel extra in SQLCOLUMNS are
                                        VoltageLevel_MemberOf_Substation

                             */
                            #endregion

                            log = modelLoadResult.Report.ToString();
                            db.SaveChanges();

                            db.Configuration.AutoDetectChangesEnabled = true; //for resetting the performance
                            db.Configuration.ValidateOnSaveEnabled = true; //for resetting the performance
                        }
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine(modelLoadResult.Report);
                        Console.ResetColor();
                    }
                }
                catch (Exception e)
                {
                    log = e.Message;
                }
                finally
                {
                    Thread.CurrentThread.CurrentCulture = culture;
                }

                File.WriteAllText("logfile.txt", log);
            }

        }

        private static Dictionary<string, Dictionary<string, Dictionary<string, string>>> FindWindingsFromTrafo(CIMModel cim)
        {
            Dictionary<string, Dictionary<string, Dictionary<string, string>>> trafowindingsdict = new Dictionary<string, Dictionary<string, Dictionary<string, string>>>();
            SortedDictionary<string, CIMObject> allWindings = cim.GetAllObjectsOfType(CIMConstants.TypeNameTransformerWinding);
            if (allWindings != null)
            {
                foreach (string windingid in allWindings.Keys)
                {
                    try
                    {
                        CIMObject winding = allWindings[windingid];
                        if (winding != null)
                        {
                            string _windid = winding.ID;
                            string _windnm = winding.Name;
                            string _windty = winding.GetAttributeList("TransformerWinding.windingType")?.FirstOrDefault()?.ValueOfReference.Split('.').LastOrDefault();
                            string _windtr = winding.GetAttributeList("TransformerWinding.MemberOf_PowerTransformer")?.FirstOrDefault()?.ValueOfReference;
                            if (_windtr == null)
                            {
                                //foundin cim 15
                                _windtr = winding.GetAttributeList("TransformerWinding.PowerTransformer")?.FirstOrDefault()?.ValueOfReference;
                            }
                            if (_windtr != null)
                            {
                                if (!trafowindingsdict.ContainsKey(_windtr))
                                    trafowindingsdict[_windtr] = new Dictionary<string, Dictionary<string, string>>();
                                if (!string.IsNullOrEmpty(_windty))
                                    trafowindingsdict[_windtr][_windty] = new Dictionary<string, string> { { "windid", _windid }, { "windnm", _windnm } }; //.Add(equiptermnodedictvalue);
                            }
                        }
                    }
                    catch (Exception ex2)
                    {
                    }
                }
                return trafowindingsdict;
            }
            else
            {
                //if there is no winding avaialble may be transfomer is directly connected to transformerend in the cim format (new cim 16 format)
                SortedDictionary<string, CIMObject> allTrafoEnds = cim.GetAllObjectsOfType(CIMConstants.TypeNamePowerTransformerEnd);
                if (allTrafoEnds != null)
                {
                    foreach (string trafoendid in allTrafoEnds.Keys)
                    {
                        try
                        {
                            CIMObject trafoend = allTrafoEnds[trafoendid];
                            if (trafoend != null)
                            {
                                string _id = trafoend.ID;
                                string _nm = trafoend.Name;
                                string _en = trafoend.GetAttributeList("TransformerEnd.endNumber")?.FirstOrDefault()?.Value;
                                string _ty = "primary";
                                //there is no direct method to know
                                if (_nm.EndsWith("_HV") || _nm.EndsWith("_LV"))
                                {
                                    _ty = _nm.EndsWith("_HV") ? "primary" : "secondary";
                                }
                                else
                                {
                                    if (_en == "0" || _en == "1")
                                    {
                                        _ty = _en == "0" ? "primary" : "secondary";
                                    }
                                    else
                                    {
                                        //find by terminal name ending with T1 or T2
                                        /*
                                                string acline_Container = cimobject.GetAttributeList("OperationalLimit.OperationalLimitType")?.FirstOrDefault()?.ValueOfReference;
                                                CIMObject vLevel = cimModel.GetObjectByID(acline_Container);
                                                if (vLevel?.CIMType == "OperationalLimitType")
                                                    value = vLevel.Name;
                                         */
                                    }
                                }
                                string _tr = trafoend.GetAttributeList("PowerTransformerEnd.PowerTransformer")?.FirstOrDefault()?.ValueOfReference;
                                if (_tr != null)
                                {
                                    if (!trafowindingsdict.ContainsKey(_tr))
                                        trafowindingsdict[_tr] = new Dictionary<string, Dictionary<string, string>>();
                                    if (!string.IsNullOrEmpty(_ty))
                                        trafowindingsdict[_tr][_ty] = new Dictionary<string, string> { { "windid", _id }, { "windnm", _nm } }; //.Add(equiptermnodedictvalue);
                                }
                            }
                        }
                        catch (Exception ex2)
                        {
                        }
                    }
                    return trafowindingsdict;
                }
            }

            return null;
        }

        private static Dictionary<string, List<Dictionary<string, string>>> FindTermNodesFromEquips(CIMModel cim)
        {
            Dictionary<string, string> nodeidname = new Dictionary<string, string>();
            SortedDictionary<string, CIMObject> allNodes = cim.GetAllObjectsOfType(CIMConstants.TypeNameConnectivityNode);
            if (allNodes != null)
            {
                foreach (string nodeId in allNodes.Keys)
                {
                    CIMObject node = allNodes[nodeId];

                    if (node != null)
                    {
                        string _nodeid = node.ID;
                        string _nodenm = node.Name;
                        nodeidname[_nodeid] = _nodenm;
                    }
                }
            }

            Dictionary<string, List<Dictionary<string, string>>> equiptermnodedict = new Dictionary<string, List<Dictionary<string, string>>>();
            SortedDictionary<string, CIMObject> allTerminals = cim.GetAllObjectsOfType(CIMConstants.TypeNameTerminal);
            if (allTerminals != null)
            {
                foreach (string terminalId in allTerminals.Keys)
                {
                    try
                    {
                        CIMObject terminal = allTerminals[terminalId];

                        if (terminal != null)
                        {
                            string _termid = terminal.ID;
                            string _termnm = terminal.Name;
                            string _nodeid = terminal.GetAttributeList("Terminal.ConnectivityNode")?.FirstOrDefault()?.ValueOfReference;
                            string _nodenm = "";
                            if (_nodeid != null) nodeidname.TryGetValue(_nodeid, out _nodenm);
                            string _equpid = terminal.GetAttributeList("Terminal.ConductingEquipment")?.FirstOrDefault()?.ValueOfReference;

                            if (_equpid != null)
                            {
                                if (!equiptermnodedict.ContainsKey(_equpid))
                                    equiptermnodedict[_equpid] = new List<Dictionary<string, string>>();

                                Dictionary<string, string> equiptermnodedictvalue = new Dictionary<string, string> {
                                    { "termid" , _termid},
                                    { "termnm" , _termnm},
                                    { "nodeid" , _nodeid ?? ""},
                                    { "nodenm" , _nodenm},
                                    //{ "equpid" , _equpid ?? ""},
                                };
                                equiptermnodedict[_equpid].Add(equiptermnodedictvalue);
                            }
                        }
                    }
                    catch (Exception ex2)
                    {
                    }
                }
                //before returing we can arrange in terminal 1 and terminal 2 order, 
                //it is not known how to find out terminal1, as it is not guranteed to follow some unique pattern always like ending with T1 T2 etc., so will just sort in ascending order or terminal name
                List<string> allequipids = equiptermnodedict.Keys.ToList();
                foreach (string kk in allequipids)
                {
                    List<Dictionary<string, string>> listoftreminalnodesdict = equiptermnodedict[kk];
                    //if at least 2 terminals present and both have terminal names present then
                    if (listoftreminalnodesdict.Count() >= 2 && listoftreminalnodesdict.All(x => x.ContainsKey("termnm")))
                    {
                        equiptermnodedict[kk] = listoftreminalnodesdict.OrderBy(x => x["termnm"]).ToList();
                    }
                }
                return equiptermnodedict;
            }
            return null;
        }

        private static object ConvertValue(Type propertyType, object value)
        {
            if (string.IsNullOrEmpty(value as string) && propertyType.Name.StartsWith("Nullable"))
                return null;

            if (propertyType == typeof(int) || propertyType == typeof(int?))
            {
                if (int.TryParse(value.ToString(), out int intValue))
                    value = intValue;
            }
            else if (propertyType == typeof(float) || propertyType == typeof(float?))
            {
                if (float.TryParse(value.ToString(), out float floatValue))
                    value = floatValue;
            }
            else if (propertyType == typeof(double) || propertyType == typeof(double?))
            {
                if (double.TryParse(value.ToString(), out double doubleValue))
                    value = doubleValue;
            }
            else if (propertyType == typeof(byte) || propertyType == typeof(byte?))
            {
                if (byte.TryParse(value.ToString(), out byte byteValue))
                    value = byteValue;
            }
            else if (propertyType == typeof(string))
            {
                if (value.ToString().Contains(";")) //if still any ; left out //MKM ; causes error while inserting in the sql server cells
                    value = value.ToString().Replace(";", "_");
                if (value.ToString().Contains("http:") && value.ToString().Contains("#")) //cases like phases, and connectiontypes
                    value = value.ToString().Split('#').LastOrDefault().Split('.').LastOrDefault();
                value = value.ToString();
            }
            else
            {
                // Extend Your own handled types
                //Debug.Assert(false);
            }
            return value;
        }

        //was trying something, where we can convert an object to a given type with a string as class name, but its not working now
        //public static T GetObjectAs<T>(dynamic source, T destinationType) where T : class
        //{
        //    return Convert.ChangeType(typeof(T), source) as T;
        //    /*Bar x = GetObjectAs(someBoxedType, new Bar());
        //    SomeTypeYouWant x = GetObjectAs(someBoxedType, Activator.CreateInstance(typeof("SomeTypeYouWant")));*/
        //}

    }

    internal enum TypesForPositionPoint
    {
        ACLineSegment = 1,
        Breaker = 2,
        BusbarSection = 3,
        Disconnector = 4,
        Feeder = 5,
        Fuse = 6,
        GeneratingUnit = 7,
        GroundDisconnector = 8,
        LoadBreakSwitch = 9,
        PowerTransformer = 10,
        ShuntCompensator = 11,
        Substation = 12,
        SynchronousMachine = 13,
        TapChanger = 14,
        Switch = 15,
        EnergyConsumer = 16
    }

    internal enum DGM_SwitchType
    {
        CIM_Breaker = 1,
        CIM_Disconnector = 2,
        CIM_Fuse = 3,
        CIM_LoadBreakSwitch = 4,
        CIM_Switch = 5
    }
}

namespace CIMImport
{
    using System;
    using System.Data;
    using System.Data.Entity;
    using System.Data.Entity.Core.Mapping;
    using System.Data.Entity.Core.Metadata.Edm;
    using System.Data.Entity.Infrastructure;
    using System.Data.SqlClient;
    using System.Linq;
    using System.Threading.Tasks;

    public partial class cim_import_dbEntities : DbContext
    {
        public async Task BulkInsertAllAsync<T>(IEnumerable<T> entities)
        {
            using (SqlConnection conn = new SqlConnection(base.Database.Connection.ConnectionString))
            {
                await conn.OpenAsync();
                Type t = typeof(T);
                SqlBulkCopy bulkCopy = new SqlBulkCopy(conn)
                {
                    DestinationTableName = GetTableName(t)
                };
                DataTable table = new DataTable();
                IEnumerable<PropertyInfo> properties = from p in t.GetProperties()
                                                       where p.PropertyType.IsValueType || p.PropertyType == typeof(string)
                                                       select p;
                foreach (PropertyInfo property2 in properties)
                {
                    Type propertyType = property2.PropertyType;
                    if (propertyType.IsGenericType && propertyType.GetGenericTypeDefinition() == typeof(Nullable<>))
                    {
                        propertyType = Nullable.GetUnderlyingType(propertyType);
                    }
                    table.Columns.Add(new DataColumn(property2.Name, propertyType));
                }
                foreach (T entity in entities)
                {
                    table.Rows.Add(properties.Select((PropertyInfo property) => property.GetValue(entity, null) ?? DBNull.Value).ToArray());
                }
                bulkCopy.BulkCopyTimeout = 0;
                await bulkCopy.WriteToServerAsync(table);
            }
        }

        public void BulkInsertAll<T>(IEnumerable<T> entities)
        {
            using (SqlConnection conn = new SqlConnection(base.Database.Connection.ConnectionString))
            {
                conn.Open();
                Type t = typeof(T);
                SqlBulkCopy bulkCopy = new SqlBulkCopy(conn)
                {
                    DestinationTableName = GetTableName(t)
                };
                DataTable table = new DataTable();
                IEnumerable<PropertyInfo> properties = from p in t.GetProperties()
                                                       where p.PropertyType.IsValueType || p.PropertyType == typeof(string)
                                                       select p;
                foreach (PropertyInfo property2 in properties)
                {
                    Type propertyType = property2.PropertyType;
                    if (propertyType.IsGenericType && propertyType.GetGenericTypeDefinition() == typeof(Nullable<>))
                    {
                        propertyType = Nullable.GetUnderlyingType(propertyType);
                    }
                    table.Columns.Add(new DataColumn(property2.Name, propertyType));
                }
                foreach (T entity in entities)
                {
                    table.Rows.Add(properties.Select((PropertyInfo property) => property.GetValue(entity, null) ?? DBNull.Value).ToArray());
                }
                bulkCopy.BulkCopyTimeout = 0;
                bulkCopy.WriteToServer(table);
            }
        }

        public string GetTableName(Type type)
        {
            MetadataWorkspace metadata = ((IObjectContextAdapter)this).ObjectContext.MetadataWorkspace;
            ObjectItemCollection objectItemCollection = (ObjectItemCollection)metadata.GetItemCollection(DataSpace.OSpace);
            EntityType entityType = metadata.GetItems<EntityType>(DataSpace.OSpace).Single((EntityType e) => objectItemCollection.GetClrType(e) == type);
            EntitySet entitySet = metadata.GetItems<EntityContainer>(DataSpace.CSpace).Single().EntitySets.Single((EntitySet s) => s.ElementType.Name == entityType.Name);
            EntitySetMapping mapping = metadata.GetItems<EntityContainerMapping>(DataSpace.CSSpace).Single().EntitySetMappings.Single((EntitySetMapping s) => s.EntitySet == entitySet);
            EntitySet table = mapping.EntityTypeMappings.Single().Fragments.Single().StoreEntitySet;
            return ((string)table.MetadataProperties["Table"].Value) ?? table.Name;
        }
    }
}