//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated from a template.
//
//     Manual changes to this file may cause unexpected behavior in your application.
//     Manual changes to this file will be overwritten if the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace CIMImport
{
    using System;
    using System.Collections.Generic;
    
    public partial class DGM_Transformer
    {
        public int IndexID { get; set; }
        public int CaseID { get; set; }
        public int TraceID { get; set; }
        public int BaseTransformerID { get; set; }
        public Nullable<int> FeederHeadID { get; set; }
        public Nullable<int> BranchID { get; set; }
        public Nullable<int> PrimaryNodeID { get; set; }
        public Nullable<int> SecondaryNodeID { get; set; }
        public Nullable<int> PrimaryPhase { get; set; }
        public Nullable<int> SecondaryPhase { get; set; }
        public Nullable<decimal> S_KVA { get; set; }
        public Nullable<decimal> Flow_kW { get; set; }
        public Nullable<decimal> AreaMaxV { get; set; }
        public Nullable<decimal> AreaMinV { get; set; }
        public Nullable<decimal> S_KVA_A { get; set; }
        public Nullable<decimal> S_KVA_B { get; set; }
        public Nullable<decimal> S_KVA_C { get; set; }
        public Nullable<int> ViolationNu { get; set; }
        public Nullable<decimal> MaxLoading { get; set; }
        public Nullable<System.DateTime> DateTime { get; set; }
        public Nullable<System.DateTime> AreaMaxUpdateTimeStamp { get; set; }
        public Nullable<int> VoltageRegulatorID { get; set; }
        public Nullable<bool> IsActiveAlarm { get; set; }
        public Nullable<bool> StartDemo_Log { get; set; }
    }
}
