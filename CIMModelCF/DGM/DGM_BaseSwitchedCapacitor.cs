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
    
    public partial class DGM_BaseSwitchedCapacitor
    {
        public int IndexID { get; set; }
        public int ID { get; set; }
        public string Name { get; set; }
        public Nullable<decimal> ReactivePowerNominal { get; set; }
        public Nullable<int> BaseFeederID { get; set; }
        public string ANodeName { get; set; }
        public Nullable<decimal> NominalVoltage { get; set; }
        public Nullable<bool> IsRemotelyControlled { get; set; }
        public Nullable<int> UpstreamCapacitorID { get; set; }
        public Nullable<int> UpstreamStepVoltageRegulatorID { get; set; }
        public Nullable<short> PhaseTypeID { get; set; }
        public Nullable<short> ConnectionTypeID { get; set; }
        public Nullable<int> CapacitorControllerID { get; set; }
        public Nullable<System.DateTime> StartDT { get; set; }
        public Nullable<System.DateTime> EndDT { get; set; }
        public Nullable<System.DateTime> CreateDT { get; set; }
        public Nullable<System.DateTime> TerminateDT { get; set; }
        public Nullable<int> CreateUserID { get; set; }
        public Nullable<int> TerminateUserID { get; set; }
        public Nullable<bool> IsDeleted { get; set; }
    }
}
