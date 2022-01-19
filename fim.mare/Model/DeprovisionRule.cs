// feb 16, 2016 | soren granfeldt
//	-added Deprovision class to prepare for deprov rules

using System.Collections.Generic;
using System.Xml.Serialization;

namespace FIM.MARE
{
	public enum DeprovisionOperation
	{
		[XmlEnum(Name = "Disconnect")]
		Disconnect,
		[XmlEnum(Name = "Delete")]
		Delete,
		[XmlEnum(Name = "ExplicitDisconnect")]
		ExplicitDisconnect
	}

	public class DeprovisionRule
	{

		[XmlAttribute("Name")]
		[XmlTextAttribute()]
		public string Name { get; set; }

		[XmlAttribute("Description")]
		[XmlTextAttribute()]
		public string Description { get; set; }

		[XmlAttribute("Action")]
		[XmlTextAttribute()]
		public DeprovisionOperation FinalAction { get; set; }

		[XmlElement("Option")]
		public List<DeprovisionOption> DeprovisionOptions { get; set; }

		public DeprovisionRule()
		{
			this.DeprovisionOptions = new List<DeprovisionOption>();
		}
	}

	[XmlInclude(typeof(FlowRuleDeprovisionOption)), XmlInclude(typeof(SetAttributeDeprovisionOption)), XmlInclude(typeof(MoveObjectDeprovisionOption))]
	public class DeprovisionOption
	{
		[XmlAttribute("Name")]
		public string Name { get; set; }

		[XmlAttribute("Description")]
		public string Description { get; set; }
		[XmlAttribute("Action")]
		[XmlTextAttribute()]
		public DeprovisionOperation Action { get; set; }

		[XmlElement("Conditions")]
		public Conditions Conditions { get; set; }
	}

	public class SetAttributeDeprovisionOption : DeprovisionOption
	{
		[XmlAttribute("Attribute")]
		public string AttributeName { get; set; }

		[XmlAttribute("Message")]
		public string Message { get; set; }
	}

	public class FlowRuleDeprovisionOption : DeprovisionOption
	{
		[XmlAttribute("FlowRule")]
		public string FlowRuleName { get; set; }

		[XmlAttribute("MVAttributeContainingDN")]
		public string MVAttributeContainingDN { get; set; }
	}

	public class MoveObjectDeprovisionOption : DeprovisionOption
	{
		[XmlAttribute("NewContainer")]
		public string NewContainer { get; set; }

	}
}
