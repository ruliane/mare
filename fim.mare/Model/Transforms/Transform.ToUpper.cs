using System.Collections.Generic;

namespace FIM.MARE
{
    public class ToUpper : Transform
    {
        public override object Convert(object value)
        {
            if (value.GetType() == typeof(string))
            {
                return string.IsNullOrEmpty(value as string) ? value : value.ToString().ToUpper();
            }
            else if (value.GetType() == typeof(List<object>))
            {
                List<object> result = new List<object>();
                for (int i = 0; i < ((List<object>)value).Count; i++)
                {
                    result.Add(((List<object>)value)[i].ToString().ToUpper());
                }
                return result;
            }

            return value;
        }
    }
}
