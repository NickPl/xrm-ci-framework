﻿using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Client;
using Microsoft.Xrm.Sdk.Query;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xrm.Framework.CI.PowerShell.Cmdlets.Common;

namespace Xrm.Framework.CI.PowerShell.Cmdlets
{
    public class PluginRegistrationHelper
    {
        private IOrganizationService OrganizationService;
        private CIContext context;

        public PluginRegistrationHelper(IOrganizationService service, CIContext xrmContext)
        {
            this.OrganizationService = service;
            this.context = xrmContext;
        }

        private void AddComponentToSolution(Guid componentId, int componentType, string solutionName)
        {
            if (string.IsNullOrEmpty(solutionName))
            {
                return;
            }

            var request = new AddSolutionComponentRequest
            {
                AddRequiredComponents = false,
                ComponentId = componentId,
                ComponentType = componentType,
                SolutionUniqueName = solutionName
            };

            OrganizationService.Execute(request);
        }

        public Guid UpsertPluginAssembly(Assembly pluginAssembly, string pluginAssemblyName, string version, string content, string solutionName, bool isWorkflowActivity, string registrationType)
        {
            Guid Id = GetPluginAssemblyId(pluginAssemblyName);

            var assembly = new PluginAssembly()
            {
                Version = version,
                Content = content,
                Name = pluginAssemblyName,
                SourceType = new OptionSetValue(0),
                IsolationMode = new OptionSetValue(2),
            };

            if (pluginAssembly != null)
            {
                assembly.SourceType = new OptionSetValue((int)GetEnumValue<PluginAssembly_SourceType>(pluginAssembly.SourceType));
                assembly.IsolationMode = new OptionSetValue((int)GetEnumValue<PluginAssembly_IsolationMode>(pluginAssembly.IsolationMode));
            }

            if (!Id.Equals(Guid.Empty) && registrationType == "reset")
            {
                DeleteDependenciesAndParent(Id, PluginAssembly.EntityLogicalName, 91);                
            }

            Id = ExecuteRequest(registrationType, Id, assembly);

            AddComponentToSolution(Id, 91, solutionName);

            return Id;
        }

        private Guid ExecuteRequest(string registrationType, Guid Id, Entity entity)
        {
            if (registrationType == "upsert" && !Id.Equals(Guid.Empty))
            {
                entity.Id = Id;
                OrganizationService.Update(entity);
            }
            else
            {
                Id = OrganizationService.Create(entity);
            }

            return Id;
        }

        private TEnum GetEnumValue<TEnum>(string name) where TEnum : struct
        {
            if (!Enum.TryParse(name, true, out TEnum enumValue))
            {
                throw new Exception(string.Format("Invalid json mapping for value {0}", name));
            }

            return enumValue;
        }

        public void DeleteDependenciesAndParent(Guid objectId, string entityLogicalName, int dependencieComponentType)
        {
            RetrieveDependenciesForDeleteRequest retrieveDependenciesForDeleteRequest = new RetrieveDependenciesForDeleteRequest()
            {
                ComponentType = dependencieComponentType,
                ObjectId = objectId
            };
            var objectsToDelete = OrganizationService.Execute(retrieveDependenciesForDeleteRequest);
            foreach (var objectToDelete in ((EntityCollection)objectsToDelete.Results.Values.First()).Entities)
            {
                OrganizationService.Delete(SdkMessageProcessingStep.EntityLogicalName, objectToDelete.GetAttributeValue<Guid>("dependentcomponentobjectid"));
            }

            OrganizationService.Delete(entityLogicalName, objectId);
        }

        private Guid GetPluginAssemblyId(string name)
        {
            var query = from a in context.PluginAssemblySet
                        where a.Name == name
                        select a.Id;

            Guid Id = query.FirstOrDefault();

            return Id;
        }

        public Guid UpsertPluginType(Guid parentId, Type pluginType, string solutionName, string registrationType, bool isWorkflowActivity, string pluginAssemblyName)
        {
            var name = pluginType.Name;
            Guid Id = GetPluginTypeId(parentId, name);

            var type = new PluginType()
            {
                Name = name,
                Description = pluginType.Description,
                FriendlyName = pluginType.FriendlyName,
                TypeName = pluginType.TypeName,
                WorkflowActivityGroupName = isWorkflowActivity ? pluginType.WorkflowActivityGroupName ?? pluginAssemblyName : null,
                PluginAssemblyId = new EntityReference(PluginAssembly.EntityLogicalName, parentId)
            };

            Id = ExecuteRequest(registrationType, Id, type);

            return Id;
        }

        private Guid GetPluginTypeId(Guid parentId, string name)
        {
            var query = from a in context.PluginTypeSet
                        where a.PluginAssemblyId.Id == parentId && a.Name == name
                        select a.Id;

            Guid Id = query.FirstOrDefault();

            return Id;
        }

        private List<PluginType> GetPluginTypes(Guid parentId)
        {
            var query = from a in context.PluginTypeSet
                        where a.PluginAssemblyId.Id == parentId
                        select new PluginType { Id = a.Id, Name = a.Name };

            var typeList = query.ToList();

            return typeList;
        }

        public Guid UpsertSdkMessageProcessingStep(Guid parentId, Step step, string solutionName, string registrationType)
        {
            var name = step.Name;
            Guid Id = GetSdkMessageProcessingStepId(parentId, name);
            var sdkMessageId = GetSdkMessageId(step.MessageName);
            var sdkMessageFilterId = GetSdkMessageFilterId(step.PrimaryEntityName, sdkMessageId);
            var sdkMessageProcessingStep = new SdkMessageProcessingStep()
            {
                Name = name,
                Description = step.Description,
                SdkMessageId = new EntityReference(SdkMessage.EntityLogicalName, sdkMessageId),
                Configuration = step.CustomConfiguration,
                FilteringAttributes = step.FilteringAttributes,
                ImpersonatingUserId = new EntityReference(SystemUser.EntityLogicalName, GetUserId(step.ImpersonatingUserFullname)),
                Mode = new OptionSetValue((int)GetEnumValue<SdkMessageProcessingStep_Mode>(step.Mode)),
                SdkMessageFilterId = new EntityReference(SdkMessageFilter.EntityLogicalName, sdkMessageFilterId),
                Rank = step.Rank,
                Stage = new OptionSetValue((int)GetEnumValue<SdkMessageProcessingStep_Stage>(step.Stage)),
                SupportedDeployment = new OptionSetValue((int)GetEnumValue<SdkMessageProcessingStep_SupportedDeployment>(step.SupportedDeployment)),
                EventHandler = new EntityReference(PluginType.EntityLogicalName, parentId),
            };

            Id = ExecuteRequest(registrationType, Id, sdkMessageProcessingStep);

            AddComponentToSolution(Id, 92, solutionName);
            return Id;
        }

        private Guid GetUserId(string name)
        {
            var query = from users in context.SystemUserSet
                        where users.FullName == name
                        select users.Id;

            Guid Id = query.FirstOrDefault();

            return Id;
        }

        public Guid GetSolutionId(string name)
        {
            var query = from solution in context.SolutionSet
                        where solution.UniqueName == name
                        select solution.Id;

            Guid Id = query.FirstOrDefault();

            return Id;
        }

        private Guid GetSdkMessageProcessingStepId(Guid parentId, string name)
        {
            var query = from steps in context.SdkMessageProcessingStepSet
                        where steps.EventHandler.Id == parentId && steps.Name == name
                        select steps.Id;

            Guid Id = query.FirstOrDefault();

            return Id;
        }

        private List<SdkMessageProcessingStep> GetSdkMessageProcessingSteps(Guid parentId)
        {
            var query = from step in context.SdkMessageProcessingStepSet
                        where step.EventHandler.Id == parentId
                        select new SdkMessageProcessingStep { Id = step.Id, Name = step.Name };

            var list = query.ToList();

            return list;
        }

        private Guid GetSdkMessageId(string name)
        {
            try
            {
                //GET SDK MESSAGE QUERY
                var query = from a in context.SdkMessageSet
                            where a.Name == name
                            select a.Id;

                Guid Id = query.FirstOrDefault();

                if (Id == null || Id == Guid.Empty)
                {
                    //throw new ItemNotFoundException(string.Format("{0} was not found", assemblyName));
                    throw new Exception(string.Format("{0} was not found", name));
                }

                return Id;
            }
            catch (InvalidPluginExecutionException invalidPluginExecutionException)
            {
                throw invalidPluginExecutionException;
            }
            catch (Exception exception)
            {
                throw exception;
            }
        }

        private Guid GetSdkMessageFilterId(string EntityLogicalName, Guid sdkMessageId)
        {
            try
            {
                //GET SDK MESSAGE FILTER QUERY
                var query = from a in context.SdkMessageFilterSet
                            where a.PrimaryObjectTypeCode == EntityLogicalName && a.SdkMessageId.Id == sdkMessageId
                            select a.Id;

                Guid Id = query.FirstOrDefault();

                return Id;
            }
            catch (InvalidPluginExecutionException invalidPluginExecutionException)
            {
                throw invalidPluginExecutionException;
            }
            catch (Exception exception)
            {
                throw exception;
            }
        }

        public Guid UpsertSdkMessageProcessingStepImage(Guid parentId, Image image, string solutionName, string registrationType)
        {
            var name = image.EntityAlias;
            var imageType = (int)GetEnumValue<SdkMessageProcessingStepImage_ImageType>(image.ImageType);
            Guid Id = GetSdkMessageProcessingStepImageId(parentId, name, imageType);

            var sdkMessageProcessingStepImage = new SdkMessageProcessingStepImage()
            {
                Attributes1 = image.Attributes,
                EntityAlias = image.EntityAlias,
                MessagePropertyName = image.MessagePropertyName,
                ImageType = new OptionSetValue(imageType),
                SdkMessageProcessingStepId = new EntityReference(SdkMessageProcessingStep.EntityLogicalName, parentId)
            };

            Id = ExecuteRequest(registrationType, Id, sdkMessageProcessingStepImage);

            return Id;
        }

        private Guid GetSdkMessageProcessingStepImageId(Guid parentId, string name, int? imageType)
        {
            var query = from a in context.SdkMessageProcessingStepImageSet
                        where a.SdkMessageProcessingStepId.Id == parentId && a.ImageType.Value == imageType && a.EntityAlias == name
                        select a.Id;

            Guid Id = query.FirstOrDefault();

            return Id;
        }

        public string GetWorkflowActivityJsonMappingFromCrm(string assemblyName, Guid solutionId)
        {
            var lastIndex = assemblyName.LastIndexOf(".dll");
            string name = lastIndex > 0 ? assemblyName.Remove(lastIndex, 4) : assemblyName;
            var pluginAssemblyList = new List<Assembly>();
            var pluginAssembliesTypes = (from pluginAssembly in context.PluginAssemblySet
                                         join plugins in context.PluginTypeSet on pluginAssembly.Id equals plugins.PluginAssemblyId.Id
                                         where (pluginAssembly.Name == name && pluginAssembly.SolutionId == solutionId) || pluginAssembly.Name == name
                                         select MapPluginObject(pluginAssemblyList, pluginAssembly, plugins, null, null, null, null, true)).ToList();
            string json = JsonConvert.SerializeObject(pluginAssemblyList.FirstOrDefault());
            return json;
        }

        public string GetPluginJsonMappingFromCrm(string assemblyName, Guid solutionId)
        {
            var lastIndex = assemblyName.LastIndexOf(".dll");
            string name = lastIndex > 0 ? assemblyName.Remove(lastIndex, 4) : assemblyName;
            var pluginAssemblyList = new List<Assembly>();
            var pluginStepImages = (from pluginAssembly in context.PluginAssemblySet
                                    join plugins in context.PluginTypeSet on pluginAssembly.Id equals plugins.PluginAssemblyId.Id
                                    join steps in context.SdkMessageProcessingStepSet on plugins.PluginTypeId equals steps.EventHandler.Id
                                    join images in context.SdkMessageProcessingStepImageSet on steps.SdkMessageProcessingStepId equals images.SdkMessageProcessingStepId.Id
                                    where (pluginAssembly.Name == name && pluginAssembly.SolutionId == solutionId) || pluginAssembly.Name == name
                                    select images).ToList();
            var pluginAssembliesTypes = (from pluginAssembly in context.PluginAssemblySet
                                         join plugins in context.PluginTypeSet on pluginAssembly.Id equals plugins.PluginAssemblyId.Id
                                         join steps in context.SdkMessageProcessingStepSet on plugins.PluginTypeId equals steps.EventHandler.Id
                                         join message in context.SdkMessageSet on steps.SdkMessageId.Id equals message.SdkMessageId
                                         join filters in context.SdkMessageFilterSet on steps.SdkMessageFilterId.Id equals filters.Id
                                         where (pluginAssembly.Name == name && pluginAssembly.SolutionId == solutionId) || pluginAssembly.Name == name
                                         select MapPluginObject(pluginAssemblyList, pluginAssembly, plugins, steps, message, filters, pluginStepImages, false)).ToList();
            string json = JsonConvert.SerializeObject(pluginAssemblyList.FirstOrDefault());
            return json;
        }

        private static Assembly MapPluginAssemblyObject(List<Assembly> pluginAssemblyList, PluginAssembly pluginAssemblies)
        {
            var pluginAssemblyTemp = pluginAssemblyList.Find(item => item.Name == pluginAssemblies.Name + ".dll");
            if (pluginAssemblyTemp == null)
            {
                pluginAssemblyTemp = new Assembly()
                {
                    Name = pluginAssemblies.Name + ".dll",
                    IsolationMode = ((PluginAssembly_IsolationMode)pluginAssemblies.IsolationMode.Value).ToString(),
                    SourceType = ((PluginAssembly_SourceType)pluginAssemblies.SourceType.Value).ToString(),
                    PluginTypes = new List<Type>(),
                };

                pluginAssemblyList.Add(pluginAssemblyTemp);
            }

            return pluginAssemblyTemp;
        }

        private static bool MapWorkflowActivityObject(List<Assembly> pluginAssemblyList
            , PluginAssembly pluginAssembly
            , PluginType pluginType
            , bool isWorkflowActivity)
        {
            var pluginAssemblyTemp = MapPluginAssemblyObject(pluginAssemblyList, pluginAssembly);
            if (pluginAssemblyTemp == null) { return false; }
            var pluginAssemblyTypeTemp = MapPluginAssemblyTypeObject(pluginType, pluginAssemblyTemp, isWorkflowActivity);

            return true;
        }

        private static bool MapPluginObject(List<Assembly> pluginAssemblyList
            , PluginAssembly pluginAssembly
            , PluginType pluginType
            , SdkMessageProcessingStep pluginStep
            , SdkMessage sdkMessage
            , SdkMessageFilter filter
            , List<SdkMessageProcessingStepImage> images
            , bool isWorkflowActivity)
        {
            var pluginAssemblyTemp = MapPluginAssemblyObject(pluginAssemblyList, pluginAssembly);
            if (pluginAssemblyTemp == null) { return false; }
            var pluginAssemblyTypeTemp = MapPluginAssemblyTypeObject(pluginType, pluginAssemblyTemp, isWorkflowActivity);

            if (pluginStep != null)
            {
                MapPluginAssemblyStepObject(pluginType, pluginStep, sdkMessage, filter, images, pluginAssemblyTemp, pluginAssemblyTypeTemp);
            }

            return true;
        }

        private static void MapPluginAssemblyStepObject(PluginType pluginType
            , SdkMessageProcessingStep pluginStep
            , SdkMessage sdkMessage
            , SdkMessageFilter filter
            , List<SdkMessageProcessingStepImage> images
            , Assembly pluginAssemblyTemp
            , Type pluginAssemblyTypeTemp)
        {
            var pluginAssemblyStepTemp = pluginAssemblyTemp.PluginTypes.FirstOrDefault(item1 => item1.Name == pluginType.Name)
                .Steps.FirstOrDefault<Step>(item => item.Name == pluginStep.Name);
            if (pluginAssemblyStepTemp == null)
            {
                pluginAssemblyStepTemp = new Step()
                {
                    CustomConfiguration = pluginStep.Configuration,
                    Name = pluginStep.Name,
                    Description = pluginStep.Description,
                    FilteringAttributes = pluginStep.FilteringAttributes,
                    ImpersonatingUserFullname = pluginStep.ImpersonatingUserId == null ? string.Empty : pluginStep.ImpersonatingUserId.Name,
                    MessageName = sdkMessage != null ? sdkMessage.CategoryName : null,
                    Mode = ((SdkMessageProcessingStep_Mode)pluginStep.Mode.Value).ToString(),
                    PrimaryEntityName = filter.PrimaryObjectTypeCode,
                    Rank = pluginStep.Rank,
                    Stage = ((SdkMessageProcessingStep_Stage)pluginStep.Stage.Value).ToString(),
                    SupportedDeployment = ((SdkMessageProcessingStep_SupportedDeployment)pluginStep.SupportedDeployment.Value).ToString(),
                    Images = new List<Image>()
                };
                MapImagesObject(images, pluginStep, pluginAssemblyStepTemp);
                pluginAssemblyTypeTemp.Steps.Add(pluginAssemblyStepTemp);

            }
        }

        private static Type MapPluginAssemblyTypeObject(PluginType pluginType, Assembly pluginAssemblyTemp, bool isWorkflowActivity)
        {
            var pluginAssemblyTypeTemp = pluginAssemblyTemp.PluginTypes.FirstOrDefault(item1 => item1.Name == pluginType.Name);

            if (pluginAssemblyTypeTemp == null)
            {
                pluginAssemblyTypeTemp = new Type()
                {
                    Description = pluginType.Description,
                    FriendlyName = pluginType.FriendlyName,
                    Name = pluginType.Name,
                    TypeName = pluginType.TypeName,
                    WorkflowActivityGroupName = isWorkflowActivity ? pluginType.WorkflowActivityGroupName ?? pluginAssemblyTemp.Name : null,
                    Steps = new List<Step>()
                };

                pluginAssemblyTemp.PluginTypes.Add(pluginAssemblyTypeTemp);
            }

            return pluginAssemblyTypeTemp;
        }

        private static Image MapImagesObject(List<SdkMessageProcessingStepImage> images, SdkMessageProcessingStep pluginStep, Step step)
        {
            Image imageTemp = null;
            var imagesTemp = images.FindAll(item => item.SdkMessageProcessingStepId.Id == pluginStep.SdkMessageProcessingStepId);
            foreach (var image in imagesTemp)
            {
                imageTemp = new Image()
                {
                    Attributes = image.Attributes1,
                    EntityAlias = image.EntityAlias,
                    MessagePropertyName = image.MessagePropertyName,
                    ImageType = image.ImageType != null ? ((SdkMessageProcessingStepImage_ImageType)image.ImageType.Value).ToString() : null
                };

                step.Images.Add(imageTemp);
            }

            return imageTemp;
        }

        public void RemoveComponentsNotInMapping(string pluginAssemblyName, Assembly pluginAssembly)
        {
            var pluginAssemblyId = GetPluginAssemblyId(pluginAssemblyName);
            var pluginTypesToDelete = GetPluginTypes(pluginAssemblyId).Where(p => !pluginAssembly.PluginTypes.Any(p2 => p2.Name == p.Name));
            foreach (var pluginType in pluginTypesToDelete)
            {
                DeleteDependenciesAndParent(pluginType.Id, pluginType.LogicalName, 90);
            }

            foreach (var pluginType in pluginAssembly.PluginTypes)
            {
                var pluginTypeId = GetPluginTypeId(pluginAssemblyId, pluginType.Name);
                var pluginStepsToDelete = GetSdkMessageProcessingSteps(pluginTypeId).Where(p => !pluginAssembly.PluginTypes.Any(p2 => p2.Name == p.Name));
                foreach (var pluginStep in pluginStepsToDelete)
                {
                    OrganizationService.Delete(pluginStep.LogicalName, pluginStep.Id);
                }
            }
        }
    }
}