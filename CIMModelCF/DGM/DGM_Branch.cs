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
    
    public partial class DGM_Branch
    {
        public int TraceID { get; set; }
        public int CaseID { get; set; }
        public int BranchID { get; set; }
        public Nullable<int> SupplyBranchID { get; set; }
        public Nullable<int> DirectionID { get; set; }
        public Nullable<int> FeederHeadID { get; set; }
        public Nullable<bool> PhaseA { get; set; }
        public Nullable<bool> PhaseB { get; set; }
        public Nullable<bool> PhaseC { get; set; }
        public Nullable<decimal> LoadFlowKW_A { get; set; }
        public Nullable<decimal> LoadFlowKW_B { get; set; }
        public Nullable<decimal> LoadFlowKW_C { get; set; }
        public Nullable<decimal> LoadFlowDRKW_A { get; set; }
        public Nullable<decimal> LoadFlowDRKW_B { get; set; }
        public Nullable<decimal> LoadFlowDRKW_C { get; set; }
    }
}