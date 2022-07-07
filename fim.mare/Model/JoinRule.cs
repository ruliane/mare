using System.Collections.Generic;
using System.Xml.Serialization;

namespace FIM.MARE
{

	public class JoinRule
    {
        [XmlAttribute("Name")]
        public string Name { get; set; }

        [XmlAttribute("Description")]
        public string Description { get; set; }

        [XmlElement("SourceExpression")]
        public SourceExpression SourceExpression { get; set; }

        public JoinRule()
		{
		}
	}

}
