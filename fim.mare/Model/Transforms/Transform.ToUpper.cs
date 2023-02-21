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
            else if (value is System.Collections.IEnumerable)
            {
                List<object> result = new List<object>();
                foreach (object o in (System.Collections.IEnumerable)value)
                {
                    result.Add(o.ToString().ToUpper());
                }
                return result;
            }
            else
            {
                return value;
            }
        }
    }
}
