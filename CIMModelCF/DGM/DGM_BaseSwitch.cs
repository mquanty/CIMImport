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
    
    public partial class DGM_BaseSwitch
    {
        public int IndexID { get; set; }
        public int ID { get; set; }
        public string Name { get; set; }
        public Nullable<int> SwitchTypeID { get; set; }
        public Nullable<int> FeederID { get; set; }
        public Nullable<int> PhaseTypeID { get; set; }
        public Nullable<bool> IsGangedOperated { get; set; }
        public Nullable<byte> NormalStatus { get; set; }
        public Nullable<decimal> NormalRatingAmp { get; set; }
        public Nullable<decimal> ShortCircuitRatingAmp { get; set; }
        public string ANodeName { get; set; }
        public string BNodeName { get; set; }
        public Nullable<System.DateTime> StartDT { get; set; }
        public Nullable<System.DateTime> EndDT { get; set; }
        public Nullable<System.DateTime> CreateDT { get; set; }
        public Nullable<System.DateTime> TerminateDT { get; set; }
        public Nullable<int> CreateUserID { get; set; }
        public Nullable<int> TerminateUserID { get; set; }
        public Nullable<bool> IsDeleted { get; set; }
        public Nullable<int> FeederHeadBusbarID { get; set; }
    }
}