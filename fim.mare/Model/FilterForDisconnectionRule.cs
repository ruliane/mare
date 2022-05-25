using System.Collections.Generic;
using System.Xml.Serialization;

namespace FIM.MARE
{

	public class FilterForDisconnectionRule
    {
        [XmlAttribute("ObjectType")]
        public string ObjectType { get; set; }

        [XmlAttribute("Description")]
        public string Description { get; set; }

        [XmlElement("Conditions")]
        public Conditions Conditions { get; set; }

        public FilterForDisconnectionRule()
		{
		}
	}

}
