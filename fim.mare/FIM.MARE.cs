using Microsoft.MetadirectoryServices;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace FIM.MARE
{

    public class RulesExtension : IMASynchronization
    {
        public Configuration config = null;

        public RulesExtension()
        {
            Tracer.TraceInformation("enter-instantiation");
            try
            {
                System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
                FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(assembly.Location);
                Tracer.TraceInformation("fim-mare-version {0}", fvi.FileVersion);

                string ConfigFileName = string.Concat(Path.GetFileNameWithoutExtension(this.GetType().Assembly.CodeBase), @".config.xml");
#if DEBUG
                string configurationFilePath = Path.Combine(System.IO.Directory.GetCurrentDirectory(), ConfigFileName);
#else
                string configurationFilePath = Path.Combine(Utils.ExtensionsDirectory, ConfigFileName);
#endif
                Tracer.TraceInformation("loading-configuration-from {0}", configurationFilePath);
                using (ConfigurationManager cfg = new ConfigurationManager())
                {
                    cfg.LoadSettingsFromFile(configurationFilePath, ref config);
                }
                Tracer.TraceInformation("loading-configuration");
                config.ManagementAgent.ForEach(ma => ma.LoadAssembly());
            }
            catch (Exception ex)
            {
                Tracer.TraceError("instantiation {0}", ex.GetBaseException());
                throw;
            }
            finally
            {
                Tracer.TraceInformation("exit-instantiation");
            }
        }
        void IMASynchronization.Initialize()
        {
            // intentionally left blank
        }
        void IMASynchronization.Terminate()
        {
            Tracer.Enter("terminate");
            try
            {
                config = null;
                Tracer.TraceInformation("pre-gc-allocated-memory '{0:n}'", GC.GetTotalMemory(true) / 1024M);
                GC.Collect();
                Tracer.TraceInformation("post-gc-allocated-memory '{0:n}'", GC.GetTotalMemory(true) / 1024M);
            }
            catch (Exception ex)
            {
                Tracer.TraceError("terminate {0}", ex.GetBaseException());
                throw;
            }
            finally
            {
                Tracer.Exit("terminate");
            }
        }

        bool IMASynchronization.ShouldProjectToMV(CSEntry csentry, out string MVObjectType)
        {
            throw new EntryPointNotImplementedException();
        }

        /* Entry point called by CSEntry filtering */
        bool IMASynchronization.FilterForDisconnection(CSEntry csentry)
        {
            Tracer.TraceInformation("enter-FilterForDisconnection");

            string maName = csentry.MA.Name;
            string objectType = csentry.ObjectType;
            Tracer.TraceInformation("csentry: dn: {0}, ma: {1}", csentry.DN, maName);

            ManagementAgent ma = config.ManagementAgent.FirstOrDefault(m => m.Name.Equals(maName));
            if (ma == null)
                throw new NotImplementedException("management-agent-" + maName + "-not-found");

            FilterForDisconnectionRule rule = ma.FilterForDisconnectionRule.FirstOrDefault(r => r.ObjectType.Equals(objectType));

            if (rule == null)
                throw new DeclineMappingException("rule-" + objectType + "-not-found-on-ma-" + maName);

            return rule.Conditions.AreMet(csentry, null);

        }
        void IMASynchronization.MapAttributesForJoin(string FlowRuleName, CSEntry csentry, ref ValueCollection values)
        {
            Tracer.TraceInformation("enter-mapattributesforjoin [{0}]", FlowRuleName);

            JoinRule rule = null;
            try
            {
                string maName = csentry.MA.Name;
                Tracer.TraceInformation("csentry: dn: {0}, ma: {1}, rule: {2}", csentry.DN, maName, FlowRuleName);

                ManagementAgent ma = config.ManagementAgent.Where(m => m.Name.Equals(maName)).FirstOrDefault();
                if (ma == null) throw new NotImplementedException("management-agent-" + maName + "-not-found");
                foreach(JoinRule r in ma.JoinRule)
                {
                    Tracer.TraceInformation("found-rule {0}", r.Name);
                }
                rule = ma.JoinRule.FirstOrDefault(r => r.Name.Equals(FlowRuleName));

                if (rule == null)
                {
                    throw new DeclineMappingException("rule-'" + FlowRuleName + "'-not-found-on-ma-" + maName);
                }

                InvokeMapJoinRule(rule, csentry, ref values);

                rule = null;
            }
            catch (Exception ex)
            {
                Tracer.TraceError("mapattributesforjoin {0}", ex.GetBaseException());
                throw ex;
            }
            finally
            {
                Tracer.TraceInformation("exit-mapattributesforjoin [{0}]", FlowRuleName);
            }
        }
        bool IMASynchronization.ResolveJoinSearch(string joinCriteriaName, CSEntry csentry, MVEntry[] rgmventry, out int imventry, ref string MVObjectType)
        {
            throw new EntryPointNotImplementedException();
        }

        DeprovisionAction FromOperation(DeprovisionOperation operation)
        {
            switch (operation)
            {
                case DeprovisionOperation.Delete:
                    return DeprovisionAction.Delete;
                case DeprovisionOperation.Disconnect:
                    return DeprovisionAction.Disconnect;
                case DeprovisionOperation.ExplicitDisconnect:
                    return DeprovisionAction.ExplicitDisconnect;
                default:
                    return DeprovisionAction.Disconnect;
            }
        }

        DeprovisionAction IMASynchronization.Deprovision(CSEntry csentry)
        {
            Tracer.TraceInformation("enter-deprovision");
            try
            {
                string maName = csentry.MA.Name;
                Tracer.TraceInformation($"ma: {maName}, dn: {csentry.DN}");

                ManagementAgent ma = config.ManagementAgent.FirstOrDefault(m => m.Name.Equals(maName));
                if (ma == null) throw new NotImplementedException("management-agent-" + maName + "-not-found");

                DeprovisionRule rule = ma.DeprovisionRule;

                if (rule == null)
                {
                    Tracer.TraceWarning($"no-deprovisioningrules-defined for {ma.Name}. Disconnecting entry");
                    return DeprovisionAction.Disconnect;
                }

                if (!rule.DeprovisionOptions.Any())
                {
                    Tracer.TraceWarning($"no Action or Option for {rule.Name} Disconnecting entry");
                    return DeprovisionAction.Disconnect;
                }

                foreach (DeprovisionOption option in rule.DeprovisionOptions)
                {
                    Tracer.TraceInformation($"found-DeprovisionOption name: {option.Name} type: {option.GetType()}");

                    if (option.GetType().Equals(typeof(SetAttributeDeprovisionOption)))
                    {
                        InvokeCsEntrySetAttribute(csentry, (SetAttributeDeprovisionOption)option);
                    }
                    else if (option.GetType().Equals(typeof(FlowRuleDeprovisionOption)))
                    {
                        string MVAttributeContainingDN = ((FlowRuleDeprovisionOption)option).MVAttributeContainingDN;
                        if (string.IsNullOrWhiteSpace(MVAttributeContainingDN))
                        {
                            throw new Exception("MVAttributeContainingDN-not-defined");
                        }

                        // Get FlowRule
                        FlowRule fr = ma.FlowRule.FirstOrDefault(m => m.Name.Equals(option.Name));
                        if (fr == null) throw new NotImplementedException("FlowRule-not-found: " + (option.Name));
                        Tracer.TraceInformation("FlowRule-found {0}", 1, fr.Name);

                        // Look for MVEntry
                        Tracer.TraceInformation("Looking for MVEntry with {0} = {1}", 1, MVAttributeContainingDN, csentry.DN);
                        MVEntry mventry = Utils.FindMVEntries(MVAttributeContainingDN, csentry.DN.ToString()).FirstOrDefault();
                        
                        // Invoke Flow Rule
                        InvokeFlowRule(fr, csentry, mventry);
                    }
                    else if (option.GetType().Equals(typeof(MoveObjectDeprovisionOption)))
                    {
                        string NewContainer = ((MoveObjectDeprovisionOption)option).NewContainer;
                        if (string.IsNullOrWhiteSpace(NewContainer))
                        {
                            throw new InvalidDNException("NewContainer-not-defined");
                        }
                        ReferenceValue ExpectedDN;
                        ExpectedDN = csentry.MA.EscapeDNComponent(csentry.RDN).Concat(NewContainer);
                        Tracer.TraceInformation("Setting new DN: {0}", ExpectedDN.ToString());
                        csentry.DN = ExpectedDN;
                    }
                    else
                    {
                        Tracer.TraceError("Action-unknown: {0}", option.GetType());
                        //break;
                    }
                }

                return FromOperation(rule.FinalAction);
            }
            catch (Exception ex)
            {
                Tracer.TraceError("deprovision {0}", ex.GetBaseException());
                throw ex;
            }
            finally
            {
                Tracer.TraceInformation("exit-deprovision");
            }

        }

        public void InvokeCsEntrySetAttribute(CSEntry csentry, SetAttributeDeprovisionOption o)
        {
            try
            {
                var message = o.Message;
                if (string.IsNullOrEmpty(o.AttributeName))
                {
                    Tracer.TraceError($"Flag option {o.Name} missing target field");
                }
                if (string.IsNullOrEmpty(message))
                {
                    Tracer.TraceError($"Flag Message not set for {o.Name} using default message");
                    message = $"Account flagged by MIM on {DateTime.Now.ToString("yyyy-MM-dd")}";
                }

                //csentry[o.AttributeName].Value = message + " " + DateTime.Now.ToString("yyyy-MM-dd");
                Tracer.TraceInformation("Setting {0} to {1}", 1, o.AttributeName, o.Message);
                csentry[o.AttributeName].Value = message;
            }
            catch (Exception ex)
            {
                Tracer.TraceError($"Error flagging account: {ex.Message}");
                throw;
            }

            return;
        }

        /*
        public DeprovisionAction invokeCsEntryMove(CSEntry csentry, DeprovisionOption o)
        {
            Tracer.TraceWarning($"Actual OU for CSEntry : {csentry.DN.ToString()}");

            string rdn = "CN=" + csentry["cn"].Value;
            var ma = Utils.MAs[csentry.MA.Name];
            ReferenceValue dn = ma.EscapeDNComponent(rdn).Concat(o.TargetOU);
            csentry.DN = dn;

            return FromOperation(DeprovisionOperation.Move);
        }
        */

        public void MapAttributesForImportExportDetached(string FlowRuleName, CSEntry csentry, MVEntry mventry, Direction direction)
        {
            Tracer.TraceInformation("enter-mapattributesforimportexportdetached [{0}]", direction);

            List<FlowRule> rules = null;
            try
            {
                string maName = csentry.MA.Name;
                Tracer.TraceInformation("mvobjectid: {0}, ma: {1}, rule: {2}", mventry.ObjectID, maName, FlowRuleName);

                ManagementAgent ma = config.ManagementAgent.FirstOrDefault(m => m.Name.Equals(maName));
                if (ma == null) throw new NotImplementedException("management-agent-" + maName + "-not-found");
                rules = ma.FlowRule.Where(r => r.Name.Equals(FlowRuleName) && r.Direction.Equals(direction)).ToList<FlowRule>();
                if (rules.Count == 0) throw new NotImplementedException(direction.ToString() + "-rule-'" + FlowRuleName + "'-not-found-on-ma-" + maName);
                
                foreach (FlowRule r in rules) Tracer.TraceInformation("found-rule name: {0}, description: {1}", r.Name, r.Description);
                
                FlowRule rule = rules.FirstOrDefault(ru => ru.Conditions.AreMet(csentry, mventry));
                if (rule == null) throw new DeclineMappingException("no-" + direction.ToString() + "-rule-'" + FlowRuleName + "'-found-on-ma-'" + maName + "'-where-conditions-were-met");

                #region FlowRuleDefault
                if (rule.GetType().Equals(typeof(FlowRule)))
                {
                    InvokeFlowRule(rule, csentry, mventry);
                    return;
                }
                #endregion
                #region FlowRuleCode
                if (rule.GetType().Equals(typeof(FlowRuleCode)))
                {
                    InvokeFlowRuleCode(ma, rule, csentry, mventry);
                    return;
                }
                #endregion

                rule = null;
            }
            catch (DeclineMappingException dme)
            {
                Tracer.TraceInformation("mapattributesforimportexportdetached {0}", dme.GetBaseException());
            }
            catch (Exception ex)
            {
                Tracer.TraceError("mapattributesforimportexportdetached {0}", ex.GetBaseException());
                throw ex;
            }
            finally
            {
                if (rules != null)
                {
                    rules.Clear();
                    rules = null;
                }
                Tracer.TraceInformation("exit-mapattributesforimportexportdetached [{0}]", direction);
            }
        }
        void IMASynchronization.MapAttributesForImport(string FlowRuleName, CSEntry csentry, MVEntry mventry)
        {
            this.MapAttributesForImportExportDetached(FlowRuleName, csentry, mventry, Direction.Import);
        }
        void IMASynchronization.MapAttributesForExport(string FlowRuleName, MVEntry mventry, CSEntry csentry)
        {
            this.MapAttributesForImportExportDetached(FlowRuleName, csentry, mventry, Direction.Export);
        }


        public void InvokeMapJoinRule(JoinRule rule, CSEntry csentry, ref ValueCollection values)
        {
            Tracer.TraceInformation("enter-invokemapjoinrule {0}", rule.Name);
            try
            {
                object targetValue = null;
                foreach (Value value in rule.SourceExpression.Source)
                {
                    if (value.GetType().Equals(typeof(MultiValueAttribute)))
                    {
                        MultiValueAttribute attr = (MultiValueAttribute)value;
                        object mv = attr.GetValueOrDefault(Direction.Import, csentry, null);
                        targetValue = attr.Transform(mv, TransformDirection.Source);
                    }
                    if (value.GetType().Equals(typeof(Attribute)))
                    {
                        Attribute attr = (Attribute)value;
                        object concateValue = attr.GetValueOrDefault(Direction.Import, csentry, null);
                        concateValue = attr.Transform(concateValue, TransformDirection.Source);
                        targetValue = targetValue as string + concateValue;
                        attr = null;
                        continue;
                    }
                    if (value.GetType().Equals(typeof(Constant)))
                    {
                        targetValue = targetValue + ((Constant)value).Value;
                        continue;
                    }
                }
                Tracer.TraceInformation("add-invokemapjoinrule-value rule: {0}, value: '{1}'", rule.Name, targetValue);
                values.Add(targetValue as string);
            }
            catch (Exception ex)
            {
                Tracer.TraceError("invokemapjoinrule {0}", ex.GetBaseException());
                throw ex;
            }
            finally
            {
                Tracer.TraceInformation("exit-invokemapjoinrule {0}", rule.Name);
            }
        }

        public void InvokeFlowRule(FlowRule rule, CSEntry csentry, MVEntry mventry)
        {
            Tracer.TraceInformation("enter-invokeflowrule {0}", rule.Name);
            try
            {
                FlowRule r = (FlowRule)rule;
                // Initiate an empty list of values
                List<object> listTargetValue = new List<object>();

                foreach (Value source in r.SourceExpression.Source)
                {
                    if (source.GetType().Equals(typeof(MultiValueAttribute)))
                    {
                        // Step 1: Get Source object and value
                        object val = null;
                        if (string.IsNullOrEmpty(source.FromOtherConnector))
                        {
                            Tracer.TraceInformation("Getting values from local connector, MultiValueAttribute {0}...", ((MultiValueAttribute)source).Name);
                            val = ((MultiValueAttribute)source).GetValueOrDefault(r.Direction, csentry, mventry);
                        }
                        else
                        {
                            // Lookup csentry in other CS
                            Tracer.TraceInformation("Getting values from connector {0}, MultiValueAttribute {1}", source.FromOtherConnector, ((MultiValueAttribute)source).Name);
                            ConnectedMA ConnectedManagementAgent = mventry.ConnectedMAs[source.FromOtherConnector];
                            if (ConnectedManagementAgent == null)
                            {
                                Tracer.TraceWarning("Unable to find external Connector {0}", 1, source.FromOtherConnector);
                                continue;
                            }

                            Tracer.TraceInformation("Found {0} connector(s) in MA {1}", ConnectedManagementAgent.Connectors.Count, source.FromOtherConnector);
                            // In some cases, several connectors might be connected to a single metaverse object so we get one of them
                            foreach (CSEntry ConnectorSpaceEntry in ConnectedManagementAgent.Connectors)
                            {
                                Tracer.TraceInformation("Reading attribute {1} on {0}", ConnectorSpaceEntry.ToString(), ((MultiValueAttribute)source).Name);
                                val = ((MultiValueAttribute)source).GetValueOrDefault(r.Direction, ConnectorSpaceEntry, mventry);
                            }
                        }

                        // Step 2: Apply Transforms on Source Attribute
                        if (val != null)
                        {
                            // Step 2: Apply Transforms on Source Attribute
                            Tracer.TraceInformation("Applying transforms on source MultiValueAttribute");
                            Object concateValue = source.Transform(val, TransformDirection.Source);

                            /* Tracer.TraceInformation("IsEnum: {0}, {1}, {2}, {3}, {4}", 
                                typeof(System.Collections.IEnumerable).IsAssignableFrom(concateValue.GetType()), 
                                typeof(System.Collections.IList).IsAssignableFrom(concateValue.GetType()), 
                                concateValue.GetType().IsEnum,
                                concateValue is System.Collections.IEnumerable,
                                (concateValue as System.Collections.IEnumerable != null)
                            ); */

                            // Step 3: Add (transformed) value to list
                            Tracer.TraceInformation("Appending values to target attribute...");
                            // After Transform, concateValue can still be a list but it's not guaranteed. Let's check that.
                            if (!(concateValue is System.Collections.IEnumerable) || concateValue is string)
                            {
                                listTargetValue.Add(concateValue);
                            }
                            else
                            {
                                foreach (object v in (System.Collections.IEnumerable)concateValue)
                                {
                                    listTargetValue.Add(v.ToString());
                                }
                            }
                        }
                        else
                        {
                            Tracer.TraceInformation("No value found.");
                        }
                    }
                    else if (source.GetType().Equals(typeof(Attribute)))
                    {
                        // Step 1: Get Source object and value
                        string val = null;
                        if (string.IsNullOrEmpty(source.FromOtherConnector))
                        {
                            Tracer.TraceInformation("Getting value from local connector, Attribute {0}...", ((Attribute)source).Name);
                            val = ((Attribute)source).GetValueOrDefault(r.Direction, csentry, mventry);
                        }
                        else
                        {
                            // Lookup csentry in other CS
                            ConnectedMA ConnectedManagementAgent = mventry.ConnectedMAs[source.FromOtherConnector];
                            if (ConnectedManagementAgent == null)
                            {
                                Tracer.TraceWarning("Unable to find external Connector {0}", 1, source.FromOtherConnector);
                                continue;
                            }

                            Tracer.TraceInformation("Found {0} connector(s) in MA {1}", ConnectedManagementAgent.Connectors.Count, source.FromOtherConnector);
                            // In some cases, several connectors might be connected to a single metaverse object so we get one of them
                            foreach (CSEntry ConnectorSpaceEntry in ConnectedManagementAgent.Connectors)
                            {
                                Tracer.TraceInformation("Reading attribute {1} on {0}", ConnectorSpaceEntry.ToString(), ((Attribute)source).Name);
                                val = ((Attribute)source).GetValueOrDefault(r.Direction, ConnectorSpaceEntry, mventry);
                            }
                        }

                        if (!string.IsNullOrEmpty(val))
                        {
                            // Step 2: Apply Transforms on Source Attribute
                            Tracer.TraceInformation("Applying transforms on source Attribute...");
                            Object concateValue = source.Transform(val, TransformDirection.Source);

                            // Step 3: Add (transformed) value to list
                            Tracer.TraceInformation("Appending value {0} to target attribute...", concateValue);
                            listTargetValue.Add(concateValue.ToString());
                        }
                        else
                        {
                            Tracer.TraceInformation("No value found.");
                        }
                    }
                    else if (source.GetType().Equals(typeof(Constant)))
                    {
                        // Step 1: Get Source object and value
                        string attr = "";
                        if (string.IsNullOrEmpty(source.FromOtherConnector))
                        {
                            attr = ((Constant)source).Value;
                        }
                        else
                        {
                            // Lookup csentry in other CS
                            ConnectedMA ConnectedManagementAgent = mventry.ConnectedMAs[source.FromOtherConnector];
                            if (ConnectedManagementAgent == null)
                            {
                                Tracer.TraceInformation("Unable to find external Connector {0}", source.FromOtherConnector);
                                continue;
                            }
                            Tracer.TraceInformation("Found {0} connector(s) in MA {1}", ConnectedManagementAgent.Connectors.Count, source.FromOtherConnector);
                            if (ConnectedManagementAgent.Connectors.Count > 0)
                            {
                                attr = ((Constant)source).Value;
                            }
                        }

                        // Step 2: Apply Transforms on Source Attribute
                        // No transform on Constant

                        // Step 3: Add (transformed) value to list
                        if (!(string.IsNullOrEmpty(attr)))
                        {
                            listTargetValue.Add(attr);
                        }
                    }
                }

                // Step 4: Apply Transforms on Target
                Tracer.TraceInformation("Applying transforms on target...");
                //List<object>transformedValues = (List<object>)r.Target.Transform(listTargetValue, TransformDirection.Target);
                listTargetValue = (List<object>)r.Target.Transform(listTargetValue, TransformDirection.Target);

                // Step 5: If Target if monovalued, concatenate values to get a String
                string strValue = null;
                if (rule.Direction == Direction.Import && !(mventry[r.Target.Name].IsMultivalued) || 
                    rule.Direction == Direction.Export && !(csentry[r.Target.Name].IsMultivalued))
                {
                    Tracer.TraceInformation("Target is monovalued, concatenating values.");
                    foreach (object targetValue in listTargetValue)
                    {
                        strValue += targetValue.ToString();
                    }
                    //listTargetValue = strValue;
                }
                
                // Finally, commit target value
                if (rule.Direction == Direction.Import && mventry[r.Target.Name].IsMultivalued || 
                    rule.Direction == Direction.Export && csentry[r.Target.Name].IsMultivalued)
                {
                    Tracer.TraceInformation("Committing values {1} to target attribute {0}...", r.Target.Name, listTargetValue);
                    r.Target.SetTargetValue(r.Direction, csentry, mventry, listTargetValue);
                }
                else
                {
                    Tracer.TraceInformation("Committing string {1} to target attribute {0}...", r.Target.Name, strValue);
                    r.Target.SetTargetValue(r.Direction, csentry, mventry, strValue);
                }
                r = null;
            }
            catch (Exception ex)
            {
                Tracer.TraceError("invokeflowrule {0}", ex.GetBaseException());
                throw ex;
            }
            finally
            {
                Tracer.TraceInformation("exit-invokeflowrule {0}", rule.Name);
            }
        }
        public void InvokeFlowRuleCode(ManagementAgent ma, FlowRule rule, CSEntry csentry, MVEntry mventry)
        {
            Tracer.TraceInformation("enter-invokeflowrulecode {0}", rule.Name);
            try
            {
                FlowRuleCode r = (FlowRuleCode)rule;
                if (r.Direction.Equals(Direction.Import))
                    ma.InvokeMapAttributesForImport(r.Name, csentry, mventry);
                else
                    ma.InvokeMapAttributesForExport(r.Name, csentry, mventry);
                r = null;
            }
            catch (Exception ex)
            {
                Tracer.TraceError("invokeflowrulecode {0}", ex.GetBaseException());
                throw ex;
            }
            finally
            {
                Tracer.TraceInformation("exit-invokeflowrulecode {0}", rule.Name);
            }
        }
    }
}
