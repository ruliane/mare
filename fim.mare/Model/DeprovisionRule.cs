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
		ExplicitDisconnect,
		[XmlEnum(Name = "Disable")]
		Disable,
		[XmlEnum(Name = "Flag")]
		Flag,
		[XmlEnum(Name = "Move")]
		Move,

	}

	public class DeprovisionRule
	{

		[XmlAttribute("Name")]
		[XmlTextAttribute()]
		public string Name { get; set; }

		[XmlAttribute("Description")]
		[XmlTextAttribute()]
		public string Description { get; set; }

		[XmlAttribute("Operation")]
		[XmlTextAttribute()]
		public DeprovisionOperation Operation { get; set; }

		[XmlElement("Option")]
		public List<DeprovisionOption> DeprovisionOptions { get; set; }

		public DeprovisionRule()
		{
			this.DeprovisionOptions = new List<DeprovisionOption>();
		}
	}

	public class DeprovisionOption
	{
		[XmlAttribute("Name")]
		public string Name { get; set; }

		[XmlAttribute("Description")]
		public string Description { get; set; }
		[XmlAttribute("Action")]
		[XmlTextAttribute()]
		public DeprovisionOperation Action { get; set; }

		[XmlAttribute("TargetOU")]
		public string TargetOU { get; set; }

		[XmlAttribute("FlagMessage")]
		public string FlagMessage { get; set; }

		[XmlAttribute("FlagField")]
		public string FlagField { get; set; }

		[XmlElement("Conditions")]
		public Conditions Conditions { get; set; }
	}
}
