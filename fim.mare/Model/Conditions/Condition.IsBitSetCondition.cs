using System;
using System.Xml.Serialization;
using Microsoft.MetadirectoryServices;

namespace FIM.MARE
{
	public class IsBitSetCondition : ConditionBase
	{
        [XmlAttribute("BitPosition")]
        public int BitPosition { get; set; }
		

        public override bool IsMet(CSEntry csentry, MVEntry mventry)
		{
			if (Source.Equals(EvaluateAttribute.CSEntry))
			{
				long tempnum = csentry[AttributeName].IntegerValue;
				//long tempnum = long.Parse(csentry[AttributeName].Value as string);
				long new_num = tempnum >> (this.BitPosition - 1);
				return (new_num & 1) == 1 ? true : false;
			}
			if (Source.Equals(EvaluateAttribute.MVEntry))
			{
				long tempnum = csentry[AttributeName].IntegerValue;
				//long tempnum = long.Parse(csentry[AttributeName].Value as string);
				long new_num = tempnum >> (this.BitPosition - 1);
				return (new_num & 1) == 1 ? true : false;
			}
			return false; // we should never get here
		}
	}
}