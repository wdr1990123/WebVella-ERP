﻿using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using WebVella.ERP.Api;
using WebVella.ERP.Api.Models;
using WebVella.ERP.Database;
using WebVella.ERP.Plugins;
using WebVella.ERP.Project.Models;

namespace WebVella.ERP.Project
{
	[PluginStartup]
	public class Startup
	{
		//System elements	
		// Check the SystemIds for lot's of helpful constants you may need, e.g. SystemIds.UserEntityId

		//Code snippets
		//Check out the CodeSnippets.txt file in WebVella.ERP.Web > Docs folder for code pieces on how to create or update some elements

		//Constants
		private static Guid WEBVELLA_PROJECT_PLUGIN_ID = new Guid("2a7bc24a-da6a-48f0-a0c7-7156a8ac69bd");			
		private static string WEBVELLA_PROJECT_PLUGIN_NAME = "webvella-project";
		private static Guid PROJECT_ENTITY_ID = new Guid("7821ece9-42ce-470b-84d4-afc9eb35aa32");			
		private static string PROJECT_ENTITY_NAME = "wv_project";
		private static Guid MILESTONE_ENTITY_ID = new Guid("d691b634-016c-46ef-8ba8-8c3328797497");			
		private static string MILESTONE_ENTITY_NAME = "wv_milestone";
		private static Guid TASK_ENTITY_ID = new Guid("65acced0-1650-4ff0-bbff-9937c382cd89");			
		private static string TASK_ENTITY_NAME = "wv_task";
		private static Guid BUG_ENTITY_ID = new Guid("c11655fa-e4a3-4c2b-8f1e-0a6d87cfd20c");			
		private static string BUG_ENTITY_NAME = "wv_bug";
		private static Guid ACTIVITY_ENTITY_ID = new Guid("145a489b-4dfc-4639-b473-2dedccb93ce0");			
		private static string ACTIVITY_ENTITY_NAME = "wv_project_activity";
		private static Guid TIMELOG_ENTITY_ID = new Guid("e2db7515-721f-446e-8333-6149b1ba131b");			
		private static string TIMELOG_ENTITY_NAME = "wv_timelog";
		private static Guid ATTACHMENT_ENTITY_ID = new Guid("f3dedc72-556a-4088-8278-bb5e8a8aad73");			
		private static string ATTACHMENT_ENTITY_NAME = "wv_project_attachment";
		private static Guid COMMENT_ENTITY_ID = new Guid("7a57d17e-98f0-4356-baf0-9a8798da0b99");			
		private static string COMMENT_ENTITY_NAME = "wv_project_comment";
		//webvella-crm plugin constants
		private static Guid CUSTOMER_ENTITY_ID = new Guid("90bcdb47-2cde-4137-a412-0198348fecc0");			
		private static string CUSTOMER_ENTITY_NAME = "wv_customer";


		public void Start()
		{

			var entMan = new EntityManager();
			var relMan = new EntityRelationManager();
			var recMan = new RecordManager();
			var storeSystemSettings = DbContext.Current.SettingsRepository.Read();
			var systemSettings = new SystemSettings(storeSystemSettings);

			//Open scope with a user we will use for the operations further ahead
			var user = new SecurityManager().GetUser(SystemIds.FirstUserId);
			using (SecurityContext.OpenScope(user))
			{
				//Here we need to initialize or update the environment based on the plugin requirements.
				//The default place for the plugin data is the "plugin_data" entity -> the "data" text field, which is used to store stringified JSON
				//containing the plugin settings or version

				#region << 1.Get the current ERP database version and checks for other plugin dependencies >>
				if (systemSettings.Version > 0)
				{
					//Do something if database version is not what you expect
				}

				//This plugin needs the webvella-crm plugin to be installed, so we will check this here
				var installedPlugins = new PluginService().Plugins;
				var crmPluginFound = false;
				foreach(var plugin in installedPlugins) {
					if(plugin.Name == "webvella-crm") {
						crmPluginFound = true;
						break;
					}
				}

				if(!crmPluginFound) 
					throw new Exception("'webvella-crm' plugin is required for the 'webvella-project' to operate");

				#endregion

				#region << 2.Get the current plugin settings from the database >>
				var currentPluginSettings = new PluginSettings();
				QueryObject pluginDataQueryObject = EntityQuery.QueryEQ("name", WEBVELLA_PROJECT_PLUGIN_NAME);
				var pluginDataQuery = new EntityQuery("plugin_data", "*", pluginDataQueryObject);
				var pluginDataQueryResponse = recMan.Find(pluginDataQuery);
				if (!pluginDataQueryResponse.Success)
					throw new Exception("plugin 'webvella-project' failed to get its settings due to: " + pluginDataQueryResponse.Message);

				if (pluginDataQueryResponse.Object == null || !pluginDataQueryResponse.Object.Data.Any() || pluginDataQueryResponse.Object.Data[0]["data"] == DBNull.Value)
				{
					//plugin was not installed
					currentPluginSettings.Version = 20160429;
					{
						string json = JsonConvert.SerializeObject(currentPluginSettings);
						var settingsEntityRecord = new EntityRecord();
						settingsEntityRecord["id"] = WEBVELLA_PROJECT_PLUGIN_ID;
						settingsEntityRecord["name"] = WEBVELLA_PROJECT_PLUGIN_NAME;
						settingsEntityRecord["data"] = json;
						var settingsSaveReponse = recMan.CreateRecord("plugin_data", settingsEntityRecord);
						if (!settingsSaveReponse.Success)
							throw new Exception("plugin 'webvella-project' failed to save its settings in the database due to: " + pluginDataQueryResponse.Message);
					}
				}
				else
				{
					string json = (string)((List<EntityRecord>)pluginDataQueryResponse.Object.Data)[0]["data"];
					currentPluginSettings = JsonConvert.DeserializeObject<PluginSettings>(json);
				}
				#endregion

				#region << 3. Run methods based on the current installed version of the plugin >>
				if (currentPluginSettings.Version < 20160430) /// TODO: SET THE VERSION CORRECTLY ONCE IT IS FINISHED IN ORDER TO TRIGGER
				{
					currentPluginSettings.Version = 20160430;

					#region << wv_project >>
					{
						#region << entity >>
						{
							InputEntity entity = new InputEntity();
							entity.Id = PROJECT_ENTITY_ID;
							entity.Name = PROJECT_ENTITY_NAME;
							entity.Label = "Project";
							entity.LabelPlural = "Projects";
							entity.System = true;
							entity.IconName = "product-hunt";
							entity.Weight = 2;
							entity.RecordPermissions = new RecordPermissions();
							entity.RecordPermissions.CanCreate = new List<Guid>();
							entity.RecordPermissions.CanRead = new List<Guid>();
							entity.RecordPermissions.CanUpdate = new List<Guid>();
							entity.RecordPermissions.CanDelete = new List<Guid>();
							//Create
							entity.RecordPermissions.CanCreate.Add(SystemIds.AdministratorRoleId);
							//READ
							entity.RecordPermissions.CanRead.Add(SystemIds.AdministratorRoleId);
							entity.RecordPermissions.CanRead.Add(SystemIds.RegularRoleId);
							//UPDATE
							entity.RecordPermissions.CanUpdate.Add(SystemIds.AdministratorRoleId);
		
							{
								var response = entMan.CreateEntity(entity);
								if (!response.Success)
									throw new Exception("System error 10050. Entity: " + PROJECT_ENTITY_NAME + " Field: entity creation" + " Message:" + response.Message);
							}
						}
						#endregion

						#region << name >>
						{
							InputTextField textboxField = new InputTextField();
							textboxField.Id = new Guid("d13427b7-e518-4305-b2cc-bc814a299b55");
							textboxField.Name = "name";
							textboxField.Label = "Name";
							textboxField.PlaceholderText = "";
							textboxField.Description = "";
							textboxField.HelpText = "";
							textboxField.Required = true;
							textboxField.Unique = false;
							textboxField.Searchable = true;
							textboxField.Auditable = false;
							textboxField.System = true;
							textboxField.DefaultValue = string.Empty;
							textboxField.MaxLength = null;
							textboxField.EnableSecurity = true;
							textboxField.Permissions = new FieldPermissions();
							textboxField.Permissions.CanRead = new List<Guid>();
							textboxField.Permissions.CanUpdate = new List<Guid>();
							//READ
							textboxField.Permissions.CanRead.Add(SystemIds.AdministratorRoleId);
							textboxField.Permissions.CanRead.Add(SystemIds.RegularRoleId);
							//UPDATE
							textboxField.Permissions.CanUpdate.Add(SystemIds.AdministratorRoleId);
							{
								var response = entMan.CreateField(PROJECT_ENTITY_ID, textboxField, false);
								if (!response.Success)
									throw new Exception("System error 10060. Entity: " + PROJECT_ENTITY_NAME + " Field: name" + " Message:" + response.Message);
							}
						}
						#endregion

						#region << description >>
						{
							InputHtmlField htmlField = new InputHtmlField();
							htmlField.Id = new Guid("d5e2c42c-c0b8-4f03-92e0-e91bede1e7b3");
							htmlField.Name = "description";
							htmlField.Label = "Description";
							htmlField.PlaceholderText = "";
							htmlField.Description = "";
							htmlField.HelpText = "";
							htmlField.Required = false;
							htmlField.Unique = false;
							htmlField.Searchable = false;
							htmlField.Auditable = false;
							htmlField.System = true;
							htmlField.DefaultValue = string.Empty;
							htmlField.EnableSecurity = true;
							htmlField.Permissions = new FieldPermissions();
							htmlField.Permissions.CanRead = new List<Guid>();
							htmlField.Permissions.CanUpdate = new List<Guid>();
							htmlField.Permissions.CanRead.Add(SystemIds.AdministratorRoleId);
							htmlField.Permissions.CanRead.Add(SystemIds.RegularRoleId);
							htmlField.Permissions.CanUpdate.Add(SystemIds.AdministratorRoleId);
							{
								var response = entMan.CreateField(PROJECT_ENTITY_ID, htmlField, false);
								if (!response.Success)
									throw new Exception("System error 10060. Entity: " + PROJECT_ENTITY_NAME + " Field: description" + " Message:" + response.Message);
							}
						}
						#endregion

						#region << owner_id >>
						{
							InputGuidField guidField = new InputGuidField();
							guidField.Id = new Guid("2e8589e0-3966-447d-8d21-6fd9a1250d60");
							guidField.Name = "owner_id";
							guidField.Label = "Project owner";
							guidField.PlaceholderText = "";
							guidField.Description = "";
							guidField.HelpText = "";
							guidField.Required = true;
							guidField.Unique = false;
							guidField.Searchable = false;
							guidField.Auditable = false;
							guidField.System = true;
							guidField.DefaultValue = Guid.Empty;
							guidField.GenerateNewId = false;
							guidField.EnableSecurity = true;
							guidField.Permissions = new FieldPermissions();
							guidField.Permissions.CanRead = new List<Guid>();
							guidField.Permissions.CanUpdate = new List<Guid>();
							//READ
							guidField.Permissions.CanRead.Add(SystemIds.AdministratorRoleId);
							guidField.Permissions.CanRead.Add(SystemIds.RegularRoleId);
							//UPDATE
							guidField.Permissions.CanUpdate.Add(SystemIds.AdministratorRoleId);
	
							{
								var response = entMan.CreateField(PROJECT_ENTITY_ID, guidField, false);
								if (!response.Success)
									throw new Exception("System error 10060. Entity: " + PROJECT_ENTITY_NAME + " Field: owner_id" + " Message:" + response.Message);
							}
						}
						#endregion

						#region << start_date >>
						{
							InputDateField dateField = new InputDateField();
							dateField.Id = new Guid("417ce7d7-a472-499b-8e70-43a1cb54723d");
							dateField.Name = "start_date";
							dateField.Label = "Start date";
							dateField.PlaceholderText = "";
							dateField.Description = "";
							dateField.HelpText = "";
							dateField.Required = false;
							dateField.Unique = false;
							dateField.Searchable = false;
							dateField.Auditable = false;
							dateField.System = true;
							dateField.DefaultValue = null;
							dateField.Format = "dd MMM yyyy";
							dateField.UseCurrentTimeAsDefaultValue = true;
							dateField.EnableSecurity = true;
							dateField.Permissions = new FieldPermissions();
							dateField.Permissions.CanRead = new List<Guid>();
							dateField.Permissions.CanUpdate = new List<Guid>();
							//READ
							dateField.Permissions.CanRead.Add(SystemIds.AdministratorRoleId);
							dateField.Permissions.CanRead.Add(SystemIds.RegularRoleId);
							//UPDATE
							dateField.Permissions.CanUpdate.Add(SystemIds.AdministratorRoleId);
							{
								var response = entMan.CreateField(PROJECT_ENTITY_ID, dateField, false);
								if (!response.Success)
									throw new Exception("System error 10060. Entity: " + PROJECT_ENTITY_NAME + " Field: start_date" + " Message:" + response.Message);
							}
						}
						#endregion

						#region << end_date >>
						{
							InputDateField dateField = new InputDateField();
							dateField.Id = new Guid("57198778-636d-47ec-b33e-edfc5705cc05");
							dateField.Name = "end_date";
							dateField.Label = "End date";
							dateField.PlaceholderText = "";
							dateField.Description = "";
							dateField.HelpText = "";
							dateField.Required = false;
							dateField.Unique = false;
							dateField.Searchable = false;
							dateField.Auditable = false;
							dateField.System = true;
							dateField.DefaultValue = null;
							dateField.Format = "dd MMM yyyy";
							dateField.UseCurrentTimeAsDefaultValue = true;
							dateField.EnableSecurity = true;
							dateField.Permissions = new FieldPermissions();
							dateField.Permissions.CanRead = new List<Guid>();
							dateField.Permissions.CanUpdate = new List<Guid>();
							//READ
							dateField.Permissions.CanRead.Add(SystemIds.AdministratorRoleId);
							dateField.Permissions.CanRead.Add(SystemIds.RegularRoleId);
							//UPDATE
							dateField.Permissions.CanUpdate.Add(SystemIds.AdministratorRoleId);
							{
								var response = entMan.CreateField(PROJECT_ENTITY_ID, dateField, false);
								if (!response.Success)
									throw new Exception("System error 10060. Entity: " + PROJECT_ENTITY_NAME + " Field: end_date" + " Message:" + response.Message);
							}
						}
						#endregion

						#region << priority >>
						{
							InputSelectField dropdownField = new InputSelectField();
							dropdownField.Id = new Guid("277feafe-5727-4a33-9024-153015dd06d0");
							dropdownField.Name = "priority";
							dropdownField.Label = "Priority";
							dropdownField.PlaceholderText = "";
							dropdownField.Description = "";
							dropdownField.HelpText = "";
							dropdownField.Required = true;
							dropdownField.Unique = false;
							dropdownField.Searchable = false;
							dropdownField.Auditable = false;
							dropdownField.System = true;
							dropdownField.DefaultValue = "medium";
							dropdownField.Options = new List<SelectFieldOption>
								{
									new SelectFieldOption(){ Key = "low", Value = "low" },
									new SelectFieldOption(){ Key = "medium", Value = "medium" },
									new SelectFieldOption(){ Key = "hight", Value = "hight" }
								};
							dropdownField.EnableSecurity = true;
							dropdownField.Permissions = new FieldPermissions();
							dropdownField.Permissions.CanRead = new List<Guid>();
							dropdownField.Permissions.CanUpdate = new List<Guid>();
							//READ
							dropdownField.Permissions.CanRead.Add(SystemIds.AdministratorRoleId);
							dropdownField.Permissions.CanRead.Add(SystemIds.RegularRoleId);
							//UPDATE
							dropdownField.Permissions.CanUpdate.Add(SystemIds.AdministratorRoleId);

							{
								var response = entMan.CreateField(PROJECT_ENTITY_ID, dropdownField, false);
								if (!response.Success)
									throw new Exception("System error 10060. Entity: " + PROJECT_ENTITY_NAME + " Field: priority" + " Message:" + response.Message);
							}
						}	
						#endregion

						#region << status >>
						{
							InputSelectField dropdownField = new InputSelectField();
							dropdownField.Id = new Guid("ba5698ba-fa81-4215-a5e1-17c368f504e2");
							dropdownField.Name = "status";
							dropdownField.Label = "Status";
							dropdownField.PlaceholderText = "";
							dropdownField.Description = "";
							dropdownField.HelpText = "";
							dropdownField.Required = true;
							dropdownField.Unique = false;
							dropdownField.Searchable = false;
							dropdownField.Auditable = false;
							dropdownField.System = true;
							dropdownField.DefaultValue = "draft";
							dropdownField.Options = new List<SelectFieldOption>
								{
									new SelectFieldOption(){ Key = "draft", Value = "draft" },
									new SelectFieldOption(){ Key = "in review", Value = "in review" },
									new SelectFieldOption(){ Key = "published", Value = "published" },
									new SelectFieldOption(){ Key = "archived", Value = "archived" }
								};
							dropdownField.EnableSecurity = true;
							dropdownField.Permissions = new FieldPermissions();
							dropdownField.Permissions.CanRead = new List<Guid>();
							dropdownField.Permissions.CanUpdate = new List<Guid>();
							//READ
							dropdownField.Permissions.CanRead.Add(SystemIds.AdministratorRoleId);
							dropdownField.Permissions.CanRead.Add(SystemIds.RegularRoleId);
							//UPDATE
							dropdownField.Permissions.CanUpdate.Add(SystemIds.AdministratorRoleId);

							{
								var response = entMan.CreateField(PROJECT_ENTITY_ID, dropdownField, false);
								if (!response.Success)
									throw new Exception("System error 10060. Entity: " + PROJECT_ENTITY_NAME + " Field: status" + " Message:" + response.Message);
							}
						}	
						#endregion

						#region << billable_hour_price >>
						{
							InputNumberField numberField = new InputNumberField();
							numberField.Id = new Guid("7179f4ab-0376-4ded-a334-a21ff451538e");
							numberField.Name = "billable_hour_price";
							numberField.Label = "Billable hour price";
							numberField.PlaceholderText = "";
							numberField.Description = "";
							numberField.HelpText = "";
							numberField.Required = true;
							numberField.Unique = false;
							numberField.Searchable = false;
							numberField.Auditable = false;
							numberField.System = true;
							numberField.DefaultValue = 0;
							numberField.MinValue = null;
							numberField.MaxValue = null;
							numberField.DecimalPlaces = 0;
							numberField.EnableSecurity = true;
							numberField.Permissions = new FieldPermissions();
							numberField.Permissions.CanRead = new List<Guid>();
							numberField.Permissions.CanUpdate = new List<Guid>();
							//READ
							numberField.Permissions.CanRead.Add(SystemIds.AdministratorRoleId);
							//UPDATE
							numberField.Permissions.CanUpdate.Add(SystemIds.AdministratorRoleId);
							{
								var response = entMan.CreateField(PROJECT_ENTITY_ID, numberField, false);
								if (!response.Success)
									throw new Exception("System error 10060. Entity: " + PROJECT_ENTITY_NAME + " Field: billable_hour_price" + " Message:" + response.Message);
							}
						}
						#endregion

						#region << customer_id >>
						{
							InputGuidField guidField = new InputGuidField();
							guidField.Id = new Guid("6aec7d63-56f3-4f1d-b2df-5fb62e30ab74");
							guidField.Name = "customer_id";
							guidField.Label = "Customer";
							guidField.PlaceholderText = "";
							guidField.Description = "";
							guidField.HelpText = "";
							guidField.Required = true;
							guidField.Unique = false;
							guidField.Searchable = false;
							guidField.Auditable = false;
							guidField.System = true;
							guidField.DefaultValue = Guid.Empty;
							guidField.GenerateNewId = false;
							guidField.EnableSecurity = true;
							guidField.Permissions = new FieldPermissions();
							guidField.Permissions.CanRead = new List<Guid>();
							guidField.Permissions.CanUpdate = new List<Guid>();
							//READ
							guidField.Permissions.CanRead.Add(SystemIds.AdministratorRoleId);
							//UPDATE
							guidField.Permissions.CanUpdate.Add(SystemIds.AdministratorRoleId);
	
							{
								var response = entMan.CreateField(PROJECT_ENTITY_ID, guidField, false);
								if (!response.Success)
									throw new Exception("System error 10060. Entity: " + PROJECT_ENTITY_NAME + " Field: customer_id" + " Message:" + response.Message);
							}
						}
						#endregion

						#region << user_1_n_project_owner Relation >>
						{
							var originEntity = entMan.ReadEntity(SystemIds.UserEntityId).Object;
							var targetEntity = entMan.ReadEntity(PROJECT_ENTITY_ID).Object;
							EntityRelation oneToNRelation = new EntityRelation();
							oneToNRelation.Id = new Guid("0cad07c3-73bd-4c1f-a5d6-552256f679a4");
							oneToNRelation.Name = "user_1_n_project_owner";
							oneToNRelation.Label = "user_1_n_project_owner";
							oneToNRelation.System = true;
							oneToNRelation.RelationType = EntityRelationType.OneToMany;
							oneToNRelation.OriginEntityId = originEntity.Id;
							oneToNRelation.OriginFieldId = originEntity.Fields.Single(x => x.Name == "id").Id;
							oneToNRelation.TargetEntityId = targetEntity.Id;
							oneToNRelation.TargetFieldId = targetEntity.Fields.Single(x => x.Name == "owner_id").Id;
							{
								var result = relMan.Create(oneToNRelation);
								if (!result.Success)
									throw new Exception("CREATE user_1_n_project_owner RELATION:" + result.Message);
							}
						}
						#endregion

						#region << customer_1_n_project Relation >>
						{
							var originEntity = entMan.ReadEntity(CUSTOMER_ENTITY_ID).Object;
							var targetEntity = entMan.ReadEntity(PROJECT_ENTITY_ID).Object;
							EntityRelation oneToNRelation = new EntityRelation();
							oneToNRelation.Id = new Guid("d7f1ec35-9f59-4d75-b8a2-554c7eaeab11");
							oneToNRelation.Name = "customer_1_n_project";
							oneToNRelation.Label = "customer_1_n_project";
							oneToNRelation.System = true;
							oneToNRelation.RelationType = EntityRelationType.OneToMany;
							oneToNRelation.OriginEntityId = originEntity.Id;
							oneToNRelation.OriginFieldId = originEntity.Fields.Single(x => x.Name == "id").Id;
							oneToNRelation.TargetEntityId = targetEntity.Id;
							oneToNRelation.TargetFieldId = targetEntity.Fields.Single(x => x.Name == "customer_id").Id;
							{
								var result = relMan.Create(oneToNRelation);
								if (!result.Success)
									throw new Exception("CREATE customer_1_n_project RELATION:" + result.Message);
							}
						}
						#endregion

						#region << role_n_n_project_team Relation >>
						//Relation for the team user roles for this projects
						{
							var originEntity = entMan.ReadEntity(SystemIds.RoleEntityId).Object;
							var targetEntity = entMan.ReadEntity(PROJECT_ENTITY_ID).Object;
							EntityRelation NToNRelation = new EntityRelation();
							NToNRelation.Id = new Guid("4860a4b6-d07e-416f-b548-60491607e93f");
							NToNRelation.Name = "role_n_n_project_team";
							NToNRelation.Label = "role_n_n_project_team";
							NToNRelation.System = true;
							NToNRelation.RelationType = EntityRelationType.ManyToMany;
							NToNRelation.OriginEntityId = originEntity.Id;
							NToNRelation.OriginFieldId = originEntity.Fields.Single(x => x.Name == "id").Id;
							NToNRelation.TargetEntityId = targetEntity.Id;
							NToNRelation.TargetFieldId = targetEntity.Fields.Single(x => x.Name == "id").Id;
							{
								var result = relMan.Create(NToNRelation);
								if (!result.Success)
									throw new Exception("CREATE role_n_n_project_team RELATION:" + result.Message);
							}
						}
						#endregion

						#region << role_n_n_project_customer Relation >>
						//Relation for the customer user roles for this projects
						{
							var originEntity = entMan.ReadEntity(SystemIds.RoleEntityId).Object;
							var targetEntity = entMan.ReadEntity(PROJECT_ENTITY_ID).Object;
							EntityRelation NToNRelation = new EntityRelation();
							NToNRelation.Id = new Guid("e6d75feb-3c8f-410b-9ff4-54ef8489dc2f");
							NToNRelation.Name = "role_n_n_project_customer";
							NToNRelation.Label = "role_n_n_project_customer";
							NToNRelation.System = true;
							NToNRelation.RelationType = EntityRelationType.ManyToMany;
							NToNRelation.OriginEntityId = originEntity.Id;
							NToNRelation.OriginFieldId = originEntity.Fields.Single(x => x.Name == "id").Id;
							NToNRelation.TargetEntityId = targetEntity.Id;
							NToNRelation.TargetFieldId = targetEntity.Fields.Single(x => x.Name == "id").Id;
							{
								var result = relMan.Create(NToNRelation);
								if (!result.Success)
									throw new Exception("CREATE role_n_n_project_customer RELATION:" + result.Message);
							}
						}
						#endregion

					}
					#endregion

					#region << wv_milestone >>
					{
						#region << entity >>
						{
							InputEntity entity = new InputEntity();
							entity.Id = MILESTONE_ENTITY_ID;
							entity.Name = MILESTONE_ENTITY_NAME;
							entity.Label = "Milestone";
							entity.LabelPlural = "Milestones";
							entity.System = true;
							entity.IconName = "map-signs";
							entity.Weight = 2;
							entity.RecordPermissions = new RecordPermissions();
							entity.RecordPermissions.CanCreate = new List<Guid>();
							entity.RecordPermissions.CanRead = new List<Guid>();
							entity.RecordPermissions.CanUpdate = new List<Guid>();
							entity.RecordPermissions.CanDelete = new List<Guid>();
							//Create
							entity.RecordPermissions.CanCreate.Add(SystemIds.AdministratorRoleId);
							entity.RecordPermissions.CanCreate.Add(SystemIds.RegularRoleId);
							//READ
							entity.RecordPermissions.CanRead.Add(SystemIds.AdministratorRoleId);
							entity.RecordPermissions.CanRead.Add(SystemIds.RegularRoleId);
							//UPDATE
							entity.RecordPermissions.CanUpdate.Add(SystemIds.AdministratorRoleId);
							entity.RecordPermissions.CanUpdate.Add(SystemIds.RegularRoleId);
							{
								var response = entMan.CreateEntity(entity);
								if (!response.Success)
									throw new Exception("System error 10050. Entity: " + MILESTONE_ENTITY_NAME + " Field: entity creation" + " Message:" + response.Message);
							}
						}
						#endregion

						#region << name >>
						{
							InputTextField textboxField = new InputTextField();
							textboxField.Id = new Guid("94cc3894-110a-4bb7-8c75-3e887cc83217");
							textboxField.Name = "name";
							textboxField.Label = "Name";
							textboxField.PlaceholderText = "";
							textboxField.Description = "";
							textboxField.HelpText = "";
							textboxField.Required = true;
							textboxField.Unique = false;
							textboxField.Searchable = true;
							textboxField.Auditable = false;
							textboxField.System = true;
							textboxField.DefaultValue = string.Empty;
							textboxField.MaxLength = null;
							textboxField.EnableSecurity = true;
							textboxField.Permissions = new FieldPermissions();
							textboxField.Permissions.CanRead = new List<Guid>();
							textboxField.Permissions.CanUpdate = new List<Guid>();
							//READ
							textboxField.Permissions.CanRead.Add(SystemIds.AdministratorRoleId);
							textboxField.Permissions.CanRead.Add(SystemIds.RegularRoleId);
							//UPDATE
							textboxField.Permissions.CanUpdate.Add(SystemIds.AdministratorRoleId);
							{
								var response = entMan.CreateField(MILESTONE_ENTITY_ID, textboxField, false);
								if (!response.Success)
									throw new Exception("System error 10060. Entity: " + MILESTONE_ENTITY_NAME + " Field: name" + " Message:" + response.Message);
							}
						}
						#endregion

						#region << start_date >>
						{
							InputDateField dateField = new InputDateField();
							dateField.Id = new Guid("9502a7e4-816c-433c-9f1e-6b1e2dffad62");
							dateField.Name = "start_date";
							dateField.Label = "Start date";
							dateField.PlaceholderText = "";
							dateField.Description = "";
							dateField.HelpText = "";
							dateField.Required = false;
							dateField.Unique = false;
							dateField.Searchable = false;
							dateField.Auditable = false;
							dateField.System = true;
							dateField.DefaultValue = null;
							dateField.Format = "dd MMM yyyy";
							dateField.UseCurrentTimeAsDefaultValue = true;
							dateField.EnableSecurity = true;
							dateField.Permissions = new FieldPermissions();
							dateField.Permissions.CanRead = new List<Guid>();
							dateField.Permissions.CanUpdate = new List<Guid>();
							//READ
							dateField.Permissions.CanRead.Add(SystemIds.AdministratorRoleId);
							dateField.Permissions.CanRead.Add(SystemIds.RegularRoleId);
							//UPDATE
							dateField.Permissions.CanUpdate.Add(SystemIds.AdministratorRoleId);
							{
								var response = entMan.CreateField(MILESTONE_ENTITY_ID, dateField, false);
								if (!response.Success)
									throw new Exception("System error 10060. Entity: " + MILESTONE_ENTITY_NAME + " Field: start_date" + " Message:" + response.Message);
							}
						}
						#endregion

						#region << end_date >>
						{
							InputDateField dateField = new InputDateField();
							dateField.Id = new Guid("1252a300-c871-4d79-8242-f036705cc86d");
							dateField.Name = "end_date";
							dateField.Label = "End date";
							dateField.PlaceholderText = "";
							dateField.Description = "";
							dateField.HelpText = "";
							dateField.Required = false;
							dateField.Unique = false;
							dateField.Searchable = false;
							dateField.Auditable = false;
							dateField.System = true;
							dateField.DefaultValue = null;
							dateField.Format = "dd MMM yyyy";
							dateField.UseCurrentTimeAsDefaultValue = true;
							dateField.EnableSecurity = true;
							dateField.Permissions = new FieldPermissions();
							dateField.Permissions.CanRead = new List<Guid>();
							dateField.Permissions.CanUpdate = new List<Guid>();
							//READ
							dateField.Permissions.CanRead.Add(SystemIds.AdministratorRoleId);
							dateField.Permissions.CanRead.Add(SystemIds.RegularRoleId);
							//UPDATE
							dateField.Permissions.CanUpdate.Add(SystemIds.AdministratorRoleId);
							{
								var response = entMan.CreateField(MILESTONE_ENTITY_ID, dateField, false);
								if (!response.Success)
									throw new Exception("System error 10060. Entity: " + MILESTONE_ENTITY_NAME + " Field: end_date" + " Message:" + response.Message);
							}
						}
						#endregion

						#region << project_id >>
						{
							InputGuidField guidField = new InputGuidField();
							guidField.Id = new Guid("f1540e81-be80-4fed-b0c6-1d538b1dd17a");
							guidField.Name = "project_id";
							guidField.Label = "Project";
							guidField.PlaceholderText = "";
							guidField.Description = "";
							guidField.HelpText = "";
							guidField.Required = true;
							guidField.Unique = false;
							guidField.Searchable = false;
							guidField.Auditable = false;
							guidField.System = true;
							guidField.DefaultValue = Guid.Empty;
							guidField.GenerateNewId = false;
							guidField.EnableSecurity = true;
							guidField.Permissions = new FieldPermissions();
							guidField.Permissions.CanRead = new List<Guid>();
							guidField.Permissions.CanUpdate = new List<Guid>();
							//READ
							guidField.Permissions.CanRead.Add(SystemIds.AdministratorRoleId);
							guidField.Permissions.CanRead.Add(SystemIds.RegularRoleId);
							//UPDATE
							guidField.Permissions.CanUpdate.Add(SystemIds.AdministratorRoleId);
	
							{
								var response = entMan.CreateField(MILESTONE_ENTITY_ID, guidField, false);
								if (!response.Success)
									throw new Exception("System error 10060. Entity: " + MILESTONE_ENTITY_NAME + " Field: project_id" + " Message:" + response.Message);
							}
						}
						#endregion

						#region << project_1_n_milestone Relation >>
						{
							var originEntity = entMan.ReadEntity(PROJECT_ENTITY_ID).Object;
							var targetEntity = entMan.ReadEntity(MILESTONE_ENTITY_ID).Object;
							EntityRelation oneToNRelation = new EntityRelation();
							oneToNRelation.Id = new Guid("0c446f98-eec2-40c1-9d66-8a3c2a2498e9");
							oneToNRelation.Name = "project_1_n_milestone";
							oneToNRelation.Label = "project_1_n_milestone";
							oneToNRelation.System = true;
							oneToNRelation.RelationType = EntityRelationType.OneToMany;
							oneToNRelation.OriginEntityId = originEntity.Id;
							oneToNRelation.OriginFieldId = originEntity.Fields.Single(x => x.Name == "id").Id;
							oneToNRelation.TargetEntityId = targetEntity.Id;
							oneToNRelation.TargetFieldId = targetEntity.Fields.Single(x => x.Name == "project_id").Id;
							{
								var result = relMan.Create(oneToNRelation);
								if (!result.Success)
									throw new Exception("CREATE project_1_n_milestone RELATION:" + result.Message);
							}
						}
						#endregion

					}
					#endregion

					#region << wv_task >>
					{
						#region << entity >>
						{
							InputEntity entity = new InputEntity();
							entity.Id = TASK_ENTITY_ID;
							entity.Name = TASK_ENTITY_NAME;
							entity.Label = "Task";
							entity.LabelPlural = "Tasks";
							entity.System = true;
							entity.IconName = "tasks";
							entity.Weight = 4;
							entity.RecordPermissions = new RecordPermissions();
							entity.RecordPermissions.CanCreate = new List<Guid>();
							entity.RecordPermissions.CanRead = new List<Guid>();
							entity.RecordPermissions.CanUpdate = new List<Guid>();
							entity.RecordPermissions.CanDelete = new List<Guid>();
							//Create
							entity.RecordPermissions.CanCreate.Add(SystemIds.AdministratorRoleId);
							entity.RecordPermissions.CanCreate.Add(SystemIds.RegularRoleId);
							//READ
							entity.RecordPermissions.CanRead.Add(SystemIds.AdministratorRoleId);
							entity.RecordPermissions.CanRead.Add(SystemIds.RegularRoleId);
							//UPDATE
							entity.RecordPermissions.CanUpdate.Add(SystemIds.AdministratorRoleId);
							entity.RecordPermissions.CanUpdate.Add(SystemIds.RegularRoleId);
		
							{
								var response = entMan.CreateEntity(entity);
								if (!response.Success)
									throw new Exception("System error 10050. Entity: " + TASK_ENTITY_NAME + " Field: entity creation" + " Message:" + response.Message);
							}
						}
						#endregion

						#region << number >>
						{
							InputAutoNumberField autonumberField = new InputAutoNumberField();
							autonumberField.Id = new Guid("1c7e71ce-125d-4afd-aa22-1c0a564bcb7b");
							autonumberField.Name = "number";
							autonumberField.Label = "Number";
							autonumberField.PlaceholderText = "";
							autonumberField.Description = "";
							autonumberField.HelpText = "";
							autonumberField.Required = true;
							autonumberField.Unique = true;
							autonumberField.Searchable = true;
							autonumberField.Auditable = false;
							autonumberField.System = true;
							autonumberField.DefaultValue = 0;
							autonumberField.DisplayFormat = "{0}";
							autonumberField.StartingNumber = 1;
							autonumberField.EnableSecurity = true;
							autonumberField.Permissions = new FieldPermissions();
							autonumberField.Permissions.CanRead = new List<Guid>();
							autonumberField.Permissions.CanUpdate = new List<Guid>();
							//READ
							autonumberField.Permissions.CanRead.Add(SystemIds.AdministratorRoleId);
							autonumberField.Permissions.CanRead.Add(SystemIds.RegularRoleId);
							{
								var response = entMan.CreateField(TASK_ENTITY_ID, autonumberField, false);
								if (!response.Success)
									throw new Exception("System error 10060. Entity: " + TASK_ENTITY_NAME + " Field: number" + " Message:" + response.Message);
							}
						}
						#endregion

						#region << subject >>
						{
							InputTextField textboxField = new InputTextField();
							textboxField.Id = new Guid("7843bfbd-30c1-4438-af48-ffe56b7f294a");
							textboxField.Name = "subject";
							textboxField.Label = "Subject";
							textboxField.PlaceholderText = "";
							textboxField.Description = "";
							textboxField.HelpText = "";
							textboxField.Required = true;
							textboxField.Unique = false;
							textboxField.Searchable = true;
							textboxField.Auditable = false;
							textboxField.System = true;
							textboxField.DefaultValue = string.Empty;
							textboxField.MaxLength = null;
							textboxField.EnableSecurity = true;
							textboxField.Permissions = new FieldPermissions();
							textboxField.Permissions.CanRead = new List<Guid>();
							textboxField.Permissions.CanUpdate = new List<Guid>();
							//READ
							textboxField.Permissions.CanRead.Add(SystemIds.AdministratorRoleId);
							textboxField.Permissions.CanRead.Add(SystemIds.RegularRoleId);
							//UPDATE
							textboxField.Permissions.CanUpdate.Add(SystemIds.AdministratorRoleId);
							textboxField.Permissions.CanUpdate.Add(SystemIds.RegularRoleId);
							{
								var response = entMan.CreateField(TASK_ENTITY_ID, textboxField, false);
								if (!response.Success)
									throw new Exception("System error 10060. Entity: " + TASK_ENTITY_NAME + " Field: subject" + " Message:" + response.Message);
							}
						}
						#endregion

						#region << owner_id >>
						{
							InputGuidField guidField = new InputGuidField();
							guidField.Id = new Guid("d9cfe549-5ae2-45ee-b7ee-1a14a84b0166");
							guidField.Name = "owner_id";
							guidField.Label = "Owned by";
							guidField.PlaceholderText = "";
							guidField.Description = "";
							guidField.HelpText = "";
							guidField.Required = true;
							guidField.Unique = false;
							guidField.Searchable = false;
							guidField.Auditable = false;
							guidField.System = true;
							guidField.DefaultValue = Guid.Empty;
							guidField.GenerateNewId = false;
							guidField.EnableSecurity = true;
							guidField.Permissions = new FieldPermissions();
							guidField.Permissions.CanRead = new List<Guid>();
							guidField.Permissions.CanUpdate = new List<Guid>();
							//READ
							guidField.Permissions.CanRead.Add(SystemIds.AdministratorRoleId);
							guidField.Permissions.CanRead.Add(SystemIds.RegularRoleId);
							//UPDATE
							guidField.Permissions.CanUpdate.Add(SystemIds.AdministratorRoleId);
							guidField.Permissions.CanUpdate.Add(SystemIds.RegularRoleId);
							{
								var response = entMan.CreateField(TASK_ENTITY_ID, guidField, false);
								if (!response.Success)
									throw new Exception("System error 10060. Entity: " + TASK_ENTITY_NAME + " Field: owner_id" + " Message:" + response.Message);
							}
						}
						#endregion

						#region << milestone_id >>
						{
							InputGuidField guidField = new InputGuidField();
							guidField.Id = new Guid("6bb67bc2-a09c-4785-b316-c8795696fc17");
							guidField.Name = "milestone_id";
							guidField.Label = "Release milestone";
							guidField.PlaceholderText = "";
							guidField.Description = "";
							guidField.HelpText = "";
							guidField.Required = false;
							guidField.Unique = false;
							guidField.Searchable = false;
							guidField.Auditable = false;
							guidField.System = true;
							guidField.DefaultValue = Guid.Empty;
							guidField.GenerateNewId = false;
							guidField.EnableSecurity = true;
							guidField.Permissions = new FieldPermissions();
							guidField.Permissions.CanRead = new List<Guid>();
							guidField.Permissions.CanUpdate = new List<Guid>();
							//READ
							guidField.Permissions.CanRead.Add(SystemIds.AdministratorRoleId);
							guidField.Permissions.CanRead.Add(SystemIds.RegularRoleId);
							//UPDATE
							guidField.Permissions.CanUpdate.Add(SystemIds.AdministratorRoleId);
							guidField.Permissions.CanUpdate.Add(SystemIds.RegularRoleId);
							{
								var response = entMan.CreateField(TASK_ENTITY_ID, guidField, false);
								if (!response.Success)
									throw new Exception("System error 10060. Entity: " + TASK_ENTITY_NAME + " Field: milestone_id" + " Message:" + response.Message);
							}
						}
						#endregion

						#region << description >>
						{
							InputHtmlField htmlField = new InputHtmlField();
							htmlField.Id = new Guid("a00eb247-918a-46ba-9869-8d1168ea8f45");
							htmlField.Name = "description";
							htmlField.Label = "Description";
							htmlField.PlaceholderText = "";
							htmlField.Description = "";
							htmlField.HelpText = "";
							htmlField.Required = false;
							htmlField.Unique = false;
							htmlField.Searchable = false;
							htmlField.Auditable = false;
							htmlField.System = true;
							htmlField.DefaultValue = string.Empty;
							htmlField.EnableSecurity = true;
							htmlField.Permissions = new FieldPermissions();
							htmlField.Permissions.CanRead = new List<Guid>();
							htmlField.Permissions.CanUpdate = new List<Guid>();
	
							htmlField.Permissions.CanRead.Add(SystemIds.AdministratorRoleId);
							htmlField.Permissions.CanRead.Add(SystemIds.RegularRoleId);
							
							htmlField.Permissions.CanUpdate.Add(SystemIds.AdministratorRoleId);
							htmlField.Permissions.CanUpdate.Add(SystemIds.RegularRoleId);
							{
								var response = entMan.CreateField(TASK_ENTITY_ID, htmlField, false);
								if (!response.Success)
									throw new Exception("System error 10060. Entity: " + TASK_ENTITY_NAME + " Field: description" + " Message:" + response.Message);
							}
						}
						#endregion

						#region << parent_id >>
						{
							InputGuidField guidField = new InputGuidField();
							guidField.Id = new Guid("98f6cdf5-634c-494b-9e71-3aa7ee779328");
							guidField.Name = "parent_id";
							guidField.Label = "Parent task";
							guidField.PlaceholderText = "";
							guidField.Description = "";
							guidField.HelpText = "";
							guidField.Required = false;
							guidField.Unique = false;
							guidField.Searchable = false;
							guidField.Auditable = false;
							guidField.System = true;
							guidField.DefaultValue = Guid.Empty;
							guidField.GenerateNewId = false;
							guidField.EnableSecurity = true;
							guidField.Permissions = new FieldPermissions();
							guidField.Permissions.CanRead = new List<Guid>();
							guidField.Permissions.CanUpdate = new List<Guid>();
							//READ
							guidField.Permissions.CanRead.Add(SystemIds.AdministratorRoleId);
							guidField.Permissions.CanRead.Add(SystemIds.RegularRoleId);
							//UPDATE
							guidField.Permissions.CanUpdate.Add(SystemIds.AdministratorRoleId);
							guidField.Permissions.CanUpdate.Add(SystemIds.RegularRoleId);
							{
								var response = entMan.CreateField(TASK_ENTITY_ID, guidField, false);
								if (!response.Success)
									throw new Exception("System error 10060. Entity: " + TASK_ENTITY_NAME + " Field: parent_id" + " Message:" + response.Message);
							}
						}
						#endregion

						#region << start_date >>
						{
							InputDateField dateField = new InputDateField();
							dateField.Id = new Guid("5bf852bf-5e6c-4791-bc8b-f1366440c04b");
							dateField.Name = "start_date";
							dateField.Label = "Start date";
							dateField.PlaceholderText = "";
							dateField.Description = "";
							dateField.HelpText = "";
							dateField.Required = false;
							dateField.Unique = false;
							dateField.Searchable = false;
							dateField.Auditable = false;
							dateField.System = true;
							dateField.DefaultValue = null;
							dateField.Format = "dd MMM yyyy";
							dateField.UseCurrentTimeAsDefaultValue = true;
							dateField.EnableSecurity = true;
							dateField.Permissions = new FieldPermissions();
							dateField.Permissions.CanRead = new List<Guid>();
							dateField.Permissions.CanUpdate = new List<Guid>();
							//READ
							dateField.Permissions.CanRead.Add(SystemIds.AdministratorRoleId);
							dateField.Permissions.CanRead.Add(SystemIds.RegularRoleId);
							//UPDATE
							dateField.Permissions.CanUpdate.Add(SystemIds.AdministratorRoleId);
							dateField.Permissions.CanUpdate.Add(SystemIds.RegularRoleId);
							{
								var response = entMan.CreateField(TASK_ENTITY_ID, dateField, false);
								if (!response.Success)
									throw new Exception("System error 10060. Entity: " + TASK_ENTITY_NAME + " Field: start_date" + " Message:" + response.Message);
							}
						}
						#endregion

						#region << end_date >>
						{
							InputDateField dateField = new InputDateField();
							dateField.Id = new Guid("c6dc9db2-0081-4a91-b0e2-78f9c1c45a51");
							dateField.Name = "end_date";
							dateField.Label = "End date";
							dateField.PlaceholderText = "";
							dateField.Description = "";
							dateField.HelpText = "";
							dateField.Required = false;
							dateField.Unique = false;
							dateField.Searchable = false;
							dateField.Auditable = false;
							dateField.System = true;
							dateField.DefaultValue = null;
							dateField.Format = "dd MMM yyyy";
							dateField.UseCurrentTimeAsDefaultValue = true;
							dateField.EnableSecurity = true;
							dateField.Permissions = new FieldPermissions();
							dateField.Permissions.CanRead = new List<Guid>();
							dateField.Permissions.CanUpdate = new List<Guid>();
							//READ
							dateField.Permissions.CanRead.Add(SystemIds.AdministratorRoleId);
							dateField.Permissions.CanRead.Add(SystemIds.RegularRoleId);
							//UPDATE
							dateField.Permissions.CanUpdate.Add(SystemIds.AdministratorRoleId);
							dateField.Permissions.CanUpdate.Add(SystemIds.RegularRoleId);
							{
								var response = entMan.CreateField(TASK_ENTITY_ID, dateField, false);
								if (!response.Success)
									throw new Exception("System error 10060. Entity: " + TASK_ENTITY_NAME + " Field: end_date" + " Message:" + response.Message);
							}
						}
						#endregion

						#region << priority >>
						{
							InputSelectField dropdownField = new InputSelectField();
							dropdownField.Id = new Guid("04ede478-99ec-4f7f-97af-0df3e89409b1");
							dropdownField.Name = "priority";
							dropdownField.Label = "Priority";
							dropdownField.PlaceholderText = "";
							dropdownField.Description = "";
							dropdownField.HelpText = "";
							dropdownField.Required = true;
							dropdownField.Unique = false;
							dropdownField.Searchable = false;
							dropdownField.Auditable = false;
							dropdownField.System = true;
							dropdownField.DefaultValue = "medium";
							dropdownField.Options = new List<SelectFieldOption>
								{
									new SelectFieldOption(){ Key = "low", Value = "low" },
									new SelectFieldOption(){ Key = "medium", Value = "medium" },
									new SelectFieldOption(){ Key = "hight", Value = "hight" }
								};
							dropdownField.EnableSecurity = true;
							dropdownField.Permissions = new FieldPermissions();
							dropdownField.Permissions.CanRead = new List<Guid>();
							dropdownField.Permissions.CanUpdate = new List<Guid>();
							//READ
							dropdownField.Permissions.CanRead.Add(SystemIds.AdministratorRoleId);
							dropdownField.Permissions.CanRead.Add(SystemIds.RegularRoleId);
							//UPDATE
							dropdownField.Permissions.CanUpdate.Add(SystemIds.AdministratorRoleId);
							dropdownField.Permissions.CanUpdate.Add(SystemIds.RegularRoleId);
							{
								var response = entMan.CreateField(TASK_ENTITY_ID, dropdownField, false);
								if (!response.Success)
									throw new Exception("System error 10060. Entity: " + TASK_ENTITY_NAME + " Field: priority" + " Message:" + response.Message);
							}
						}	
						#endregion

						#region << status >>
						{
							InputSelectField dropdownField = new InputSelectField();
							dropdownField.Id = new Guid("0702e611-6fe5-42e4-9bad-d549cba9cbb1");
							dropdownField.Name = "status";
							dropdownField.Label = "Status";
							dropdownField.PlaceholderText = "";
							dropdownField.Description = "";
							dropdownField.HelpText = "";
							dropdownField.Required = true;
							dropdownField.Unique = false;
							dropdownField.Searchable = false;
							dropdownField.Auditable = false;
							dropdownField.System = true;
							dropdownField.DefaultValue = "not started";
							dropdownField.Options = new List<SelectFieldOption>
								{
									new SelectFieldOption(){ Key = "not started", Value = "not started" },
									new SelectFieldOption(){ Key = "in progress", Value = "in progress" },
									new SelectFieldOption(){ Key = "completed", Value = "completed" }
								};
							dropdownField.EnableSecurity = true;
							dropdownField.Permissions = new FieldPermissions();
							dropdownField.Permissions.CanRead = new List<Guid>();
							dropdownField.Permissions.CanUpdate = new List<Guid>();
							//READ
							dropdownField.Permissions.CanRead.Add(SystemIds.AdministratorRoleId);
							dropdownField.Permissions.CanRead.Add(SystemIds.RegularRoleId);
							//UPDATE
							dropdownField.Permissions.CanUpdate.Add(SystemIds.AdministratorRoleId);
							dropdownField.Permissions.CanUpdate.Add(SystemIds.RegularRoleId);
							{
								var response = entMan.CreateField(TASK_ENTITY_ID, dropdownField, false);
								if (!response.Success)
									throw new Exception("System error 10060. Entity: " + TASK_ENTITY_NAME + " Field: status" + " Message:" + response.Message);
							}
						}	
						#endregion

						#region << billable_hours >>
						{
							InputNumberField numberField = new InputNumberField();
							numberField.Id = new Guid("d79a25e4-3eeb-4a5b-84e8-294b0c146c4d");
							numberField.Name = "billable_hours";
							numberField.Label = "Billable hours";
							numberField.PlaceholderText = "";
							numberField.Description = "";
							numberField.HelpText = "";
							numberField.Required = false;
							numberField.Unique = false;
							numberField.Searchable = false;
							numberField.Auditable = false;
							numberField.System = true;
							numberField.DefaultValue = 0;
							numberField.MinValue = null;
							numberField.MaxValue = null;
							numberField.DecimalPlaces = 0;
							numberField.EnableSecurity = true;
							numberField.Permissions = new FieldPermissions();
							numberField.Permissions.CanRead = new List<Guid>();
							numberField.Permissions.CanUpdate = new List<Guid>();
							//READ
							numberField.Permissions.CanRead.Add(SystemIds.AdministratorRoleId);
							numberField.Permissions.CanRead.Add(SystemIds.RegularRoleId);
							//UPDATE
							numberField.Permissions.CanUpdate.Add(SystemIds.AdministratorRoleId);
							numberField.Permissions.CanUpdate.Add(SystemIds.RegularRoleId);
							{
								var response = entMan.CreateField(TASK_ENTITY_ID, numberField, false);
								if (!response.Success)
									throw new Exception("System error 10060. Entity: " + TASK_ENTITY_NAME + " Field: billable_hours" + " Message:" + response.Message);
							}
						}
						#endregion

						#region << nonbillable_hours >>
						{
							InputNumberField numberField = new InputNumberField();
							numberField.Id = new Guid("a4196b7b-1de7-4106-b393-79eb3d1b4b79");
							numberField.Name = "nonbillable_hours";
							numberField.Label = "Non-Billable hours";
							numberField.PlaceholderText = "";
							numberField.Description = "";
							numberField.HelpText = "";
							numberField.Required = false;
							numberField.Unique = false;
							numberField.Searchable = false;
							numberField.Auditable = false;
							numberField.System = true;
							numberField.DefaultValue = 0;
							numberField.MinValue = null;
							numberField.MaxValue = null;
							numberField.DecimalPlaces = 0;
							numberField.EnableSecurity = true;
							numberField.Permissions = new FieldPermissions();
							numberField.Permissions.CanRead = new List<Guid>();
							numberField.Permissions.CanUpdate = new List<Guid>();
							//READ
							numberField.Permissions.CanRead.Add(SystemIds.AdministratorRoleId);
							numberField.Permissions.CanRead.Add(SystemIds.RegularRoleId);
							//UPDATE
							numberField.Permissions.CanUpdate.Add(SystemIds.AdministratorRoleId);
							numberField.Permissions.CanUpdate.Add(SystemIds.RegularRoleId);
							{
								var response = entMan.CreateField(TASK_ENTITY_ID, numberField, false);
								if (!response.Success)
									throw new Exception("System error 10060. Entity: " + TASK_ENTITY_NAME + " Field: nonbillable_hours" + " Message:" + response.Message);
							}
						}
						#endregion

						#region << user_1_n_task_owner Relation >>
						{
							var originEntity = entMan.ReadEntity(SystemIds.UserEntityId).Object;
							var targetEntity = entMan.ReadEntity(TASK_ENTITY_ID).Object;
							EntityRelation oneToNRelation = new EntityRelation();
							oneToNRelation.Id = new Guid("7ce76c81-e604-401e-907f-23de982b930e");
							oneToNRelation.Name = "user_1_n_task_owner";
							oneToNRelation.Label = "user_1_n_task_owner";
							oneToNRelation.System = true;
							oneToNRelation.RelationType = EntityRelationType.OneToMany;
							oneToNRelation.OriginEntityId = originEntity.Id;
							oneToNRelation.OriginFieldId = originEntity.Fields.Single(x => x.Name == "id").Id;
							oneToNRelation.TargetEntityId = targetEntity.Id;
							oneToNRelation.TargetFieldId = targetEntity.Fields.Single(x => x.Name == "owner_id").Id;
							{
								var result = relMan.Create(oneToNRelation);
								if (!result.Success)
									throw new Exception("CREATE user_1_n_task_owner RELATION:" + result.Message);
							}
						}
						#endregion

						#region << task_1_n_task_parent Relation >>
						{
							var originEntity = entMan.ReadEntity(TASK_ENTITY_ID).Object;
							var targetEntity = entMan.ReadEntity(TASK_ENTITY_ID).Object;
							EntityRelation oneToNRelation = new EntityRelation();
							oneToNRelation.Id = new Guid("fd42ca83-9c08-4e7d-ba97-782208f44b18");
							oneToNRelation.Name = "task_1_n_task_parent";
							oneToNRelation.Label = "task_1_n_task_parent";
							oneToNRelation.System = true;
							oneToNRelation.RelationType = EntityRelationType.OneToMany;
							oneToNRelation.OriginEntityId = originEntity.Id;
							oneToNRelation.OriginFieldId = originEntity.Fields.Single(x => x.Name == "id").Id;
							oneToNRelation.TargetEntityId = targetEntity.Id;
							oneToNRelation.TargetFieldId = targetEntity.Fields.Single(x => x.Name == "parent_id").Id;
							{
								var result = relMan.Create(oneToNRelation);
								if (!result.Success)
									throw new Exception("CREATE task_1_n_task_parent RELATION:" + result.Message);
							}
						}
						#endregion

					}
					#endregion

					#region << wv_bug >>
					{
						#region << entity >>
						{
							InputEntity entity = new InputEntity();
							entity.Id = BUG_ENTITY_ID;
							entity.Name = BUG_ENTITY_NAME;
							entity.Label = "Bug";
							entity.LabelPlural = "Bugs";
							entity.System = true;
							entity.IconName = "bug";
							entity.Weight = 5;
							entity.RecordPermissions = new RecordPermissions();
							entity.RecordPermissions.CanCreate = new List<Guid>();
							entity.RecordPermissions.CanRead = new List<Guid>();
							entity.RecordPermissions.CanUpdate = new List<Guid>();
							entity.RecordPermissions.CanDelete = new List<Guid>();
							//Create
							entity.RecordPermissions.CanCreate.Add(SystemIds.AdministratorRoleId);
							entity.RecordPermissions.CanCreate.Add(SystemIds.RegularRoleId);
							//READ
							entity.RecordPermissions.CanRead.Add(SystemIds.AdministratorRoleId);
							entity.RecordPermissions.CanRead.Add(SystemIds.RegularRoleId);
							//UPDATE
							entity.RecordPermissions.CanUpdate.Add(SystemIds.AdministratorRoleId);
							entity.RecordPermissions.CanUpdate.Add(SystemIds.RegularRoleId);
		
							{
								var response = entMan.CreateEntity(entity);
								if (!response.Success)
									throw new Exception("System error 10050. Entity: " + BUG_ENTITY_NAME + " Field: entity creation" + " Message:" + response.Message);
							}
						}
						#endregion

						#region << number >>
						{
							InputAutoNumberField autonumberField = new InputAutoNumberField();
							autonumberField.Id = new Guid("01a14364-7e42-42ed-b6aa-92525b7b36fb");
							autonumberField.Name = "number";
							autonumberField.Label = "Number";
							autonumberField.PlaceholderText = "";
							autonumberField.Description = "";
							autonumberField.HelpText = "";
							autonumberField.Required = true;
							autonumberField.Unique = true;
							autonumberField.Searchable = true;
							autonumberField.Auditable = false;
							autonumberField.System = true;
							autonumberField.DefaultValue = 0;
							autonumberField.DisplayFormat = "{0}";
							autonumberField.StartingNumber = 1;
							autonumberField.EnableSecurity = true;
							autonumberField.Permissions = new FieldPermissions();
							autonumberField.Permissions.CanRead = new List<Guid>();
							autonumberField.Permissions.CanUpdate = new List<Guid>();
							//READ
							autonumberField.Permissions.CanRead.Add(SystemIds.AdministratorRoleId);
							autonumberField.Permissions.CanRead.Add(SystemIds.RegularRoleId);
							{
								var response = entMan.CreateField(BUG_ENTITY_ID, autonumberField, false);
								if (!response.Success)
									throw new Exception("System error 10060. Entity: " + BUG_ENTITY_NAME + " Field: number" + " Message:" + response.Message);
							}
						}
						#endregion

						#region << subject >>
						{
							InputTextField textboxField = new InputTextField();
							textboxField.Id = new Guid("335a4500-130e-4739-b774-2f53f33ea22a");
							textboxField.Name = "subject";
							textboxField.Label = "Subject";
							textboxField.PlaceholderText = "";
							textboxField.Description = "";
							textboxField.HelpText = "";
							textboxField.Required = true;
							textboxField.Unique = false;
							textboxField.Searchable = true;
							textboxField.Auditable = false;
							textboxField.System = true;
							textboxField.DefaultValue = string.Empty;
							textboxField.MaxLength = null;
							textboxField.EnableSecurity = true;
							textboxField.Permissions = new FieldPermissions();
							textboxField.Permissions.CanRead = new List<Guid>();
							textboxField.Permissions.CanUpdate = new List<Guid>();
							//READ
							textboxField.Permissions.CanRead.Add(SystemIds.AdministratorRoleId);
							textboxField.Permissions.CanRead.Add(SystemIds.RegularRoleId);
							//UPDATE
							textboxField.Permissions.CanUpdate.Add(SystemIds.AdministratorRoleId);
							textboxField.Permissions.CanUpdate.Add(SystemIds.RegularRoleId);
							{
								var response = entMan.CreateField(BUG_ENTITY_ID, textboxField, false);
								if (!response.Success)
									throw new Exception("System error 10060. Entity: " + BUG_ENTITY_NAME + " Field: subject" + " Message:" + response.Message);
							}
						}
						#endregion

						#region << description >>
						{
							InputHtmlField htmlField = new InputHtmlField();
							htmlField.Id = new Guid("4afe9621-39ee-40b9-a3ef-cb9b98131a6a");
							htmlField.Name = "description";
							htmlField.Label = "Description";
							htmlField.PlaceholderText = "";
							htmlField.Description = "";
							htmlField.HelpText = "";
							htmlField.Required = false;
							htmlField.Unique = false;
							htmlField.Searchable = false;
							htmlField.Auditable = false;
							htmlField.System = true;
							htmlField.DefaultValue = string.Empty;
							htmlField.EnableSecurity = true;
							htmlField.Permissions = new FieldPermissions();
							htmlField.Permissions.CanRead = new List<Guid>();
							htmlField.Permissions.CanUpdate = new List<Guid>();
	
							htmlField.Permissions.CanRead.Add(SystemIds.AdministratorRoleId);
							htmlField.Permissions.CanRead.Add(SystemIds.RegularRoleId);
							
							htmlField.Permissions.CanUpdate.Add(SystemIds.AdministratorRoleId);
							htmlField.Permissions.CanUpdate.Add(SystemIds.RegularRoleId);
							{
								var response = entMan.CreateField(BUG_ENTITY_ID, htmlField, false);
								if (!response.Success)
									throw new Exception("System error 10060. Entity: " + BUG_ENTITY_NAME + " Field: description" + " Message:" + response.Message);
							}
						}
						#endregion

						#region << priority >>
						{
							InputSelectField dropdownField = new InputSelectField();
							dropdownField.Id = new Guid("e506dfc7-6d49-4d00-9f61-8befd56c1a6e");
							dropdownField.Name = "priority";
							dropdownField.Label = "Priority";
							dropdownField.PlaceholderText = "";
							dropdownField.Description = "";
							dropdownField.HelpText = "";
							dropdownField.Required = true;
							dropdownField.Unique = false;
							dropdownField.Searchable = false;
							dropdownField.Auditable = false;
							dropdownField.System = true;
							dropdownField.DefaultValue = "medium";
							dropdownField.Options = new List<SelectFieldOption>
								{
									new SelectFieldOption(){ Key = "low", Value = "low" },
									new SelectFieldOption(){ Key = "medium", Value = "medium" },
									new SelectFieldOption(){ Key = "hight", Value = "hight" }
								};
							dropdownField.EnableSecurity = true;
							dropdownField.Permissions = new FieldPermissions();
							dropdownField.Permissions.CanRead = new List<Guid>();
							dropdownField.Permissions.CanUpdate = new List<Guid>();
							//READ
							dropdownField.Permissions.CanRead.Add(SystemIds.AdministratorRoleId);
							dropdownField.Permissions.CanRead.Add(SystemIds.RegularRoleId);
							//UPDATE
							dropdownField.Permissions.CanUpdate.Add(SystemIds.AdministratorRoleId);
							dropdownField.Permissions.CanUpdate.Add(SystemIds.RegularRoleId);
							{
								var response = entMan.CreateField(BUG_ENTITY_ID, dropdownField, false);
								if (!response.Success)
									throw new Exception("System error 10060. Entity: " + BUG_ENTITY_NAME + " Field: priority" + " Message:" + response.Message);
							}
						}	
						#endregion

						#region << owner_id >>
						{
							InputGuidField guidField = new InputGuidField();
							guidField.Id = new Guid("0ed595db-1abd-4a74-b160-db879c33cba9");
							guidField.Name = "owner_id";
							guidField.Label = "Owned by";
							guidField.PlaceholderText = "";
							guidField.Description = "";
							guidField.HelpText = "";
							guidField.Required = true;
							guidField.Unique = false;
							guidField.Searchable = false;
							guidField.Auditable = false;
							guidField.System = true;
							guidField.DefaultValue = Guid.Empty;
							guidField.GenerateNewId = false;
							guidField.EnableSecurity = true;
							guidField.Permissions = new FieldPermissions();
							guidField.Permissions.CanRead = new List<Guid>();
							guidField.Permissions.CanUpdate = new List<Guid>();
							//READ
							guidField.Permissions.CanRead.Add(SystemIds.AdministratorRoleId);
							guidField.Permissions.CanRead.Add(SystemIds.RegularRoleId);
							//UPDATE
							guidField.Permissions.CanUpdate.Add(SystemIds.AdministratorRoleId);
							guidField.Permissions.CanUpdate.Add(SystemIds.RegularRoleId);
							{
								var response = entMan.CreateField(BUG_ENTITY_ID, guidField, false);
								if (!response.Success)
									throw new Exception("System error 10060. Entity: " + BUG_ENTITY_NAME + " Field: owner_id" + " Message:" + response.Message);
							}
						}
						#endregion

						#region << milestone_id >>
						{
							InputGuidField guidField = new InputGuidField();
							guidField.Id = new Guid("bd817922-e269-44cf-bc84-64911b5abbdd");
							guidField.Name = "milestone_id";
							guidField.Label = "Release milestone";
							guidField.PlaceholderText = "";
							guidField.Description = "";
							guidField.HelpText = "";
							guidField.Required = false;
							guidField.Unique = false;
							guidField.Searchable = false;
							guidField.Auditable = false;
							guidField.System = true;
							guidField.DefaultValue = Guid.Empty;
							guidField.GenerateNewId = false;
							guidField.EnableSecurity = true;
							guidField.Permissions = new FieldPermissions();
							guidField.Permissions.CanRead = new List<Guid>();
							guidField.Permissions.CanUpdate = new List<Guid>();
							//READ
							guidField.Permissions.CanRead.Add(SystemIds.AdministratorRoleId);
							guidField.Permissions.CanRead.Add(SystemIds.RegularRoleId);
							//UPDATE
							guidField.Permissions.CanUpdate.Add(SystemIds.AdministratorRoleId);
							guidField.Permissions.CanUpdate.Add(SystemIds.RegularRoleId);
							{
								var response = entMan.CreateField(BUG_ENTITY_ID, guidField, false);
								if (!response.Success)
									throw new Exception("System error 10060. Entity: " + BUG_ENTITY_NAME + " Field: milestone_id" + " Message:" + response.Message);
							}
						}
						#endregion

						#region << status >>
						{
							InputSelectField dropdownField = new InputSelectField();
							dropdownField.Id = new Guid("48be1e6e-df47-46f0-b4e1-6e9e1cbaf71c");
							dropdownField.Name = "status";
							dropdownField.Label = "Status";
							dropdownField.PlaceholderText = "";
							dropdownField.Description = "";
							dropdownField.HelpText = "";
							dropdownField.Required = true;
							dropdownField.Unique = false;
							dropdownField.Searchable = false;
							dropdownField.Auditable = false;
							dropdownField.System = true;
							dropdownField.DefaultValue = "opened";
							dropdownField.Options = new List<SelectFieldOption>
								{
									new SelectFieldOption(){ Key = "opened", Value = "opened" },
									new SelectFieldOption(){ Key = "closed", Value = "closed" },
									new SelectFieldOption(){ Key = "reopened", Value = "reopened" }
								};
							dropdownField.EnableSecurity = true;
							dropdownField.Permissions = new FieldPermissions();
							dropdownField.Permissions.CanRead = new List<Guid>();
							dropdownField.Permissions.CanUpdate = new List<Guid>();
							//READ
							dropdownField.Permissions.CanRead.Add(SystemIds.AdministratorRoleId);
							dropdownField.Permissions.CanRead.Add(SystemIds.RegularRoleId);
							//UPDATE
							dropdownField.Permissions.CanUpdate.Add(SystemIds.AdministratorRoleId);
							dropdownField.Permissions.CanUpdate.Add(SystemIds.RegularRoleId);
							{
								var response = entMan.CreateField(BUG_ENTITY_ID, dropdownField, false);
								if (!response.Success)
									throw new Exception("System error 10060. Entity: " + BUG_ENTITY_NAME + " Field: status" + " Message:" + response.Message);
							}
						}	
						#endregion

						#region << user_1_n_bug_owner Relation >>
						{
							var originEntity = entMan.ReadEntity(SystemIds.UserEntityId).Object;
							var targetEntity = entMan.ReadEntity(BUG_ENTITY_ID).Object;
							EntityRelation oneToNRelation = new EntityRelation();
							oneToNRelation.Id = new Guid("cddc10b6-30ff-4a86-96e4-645b3ea59fd9");
							oneToNRelation.Name = "user_1_n_bug_owner";
							oneToNRelation.Label = "user_1_n_bug_owner";
							oneToNRelation.System = true;
							oneToNRelation.RelationType = EntityRelationType.OneToMany;
							oneToNRelation.OriginEntityId = originEntity.Id;
							oneToNRelation.OriginFieldId = originEntity.Fields.Single(x => x.Name == "id").Id;
							oneToNRelation.TargetEntityId = targetEntity.Id;
							oneToNRelation.TargetFieldId = targetEntity.Fields.Single(x => x.Name == "owner_id").Id;
							{
								var result = relMan.Create(oneToNRelation);
								if (!result.Success)
									throw new Exception("CREATE user_1_n_bug_owner RELATION:" + result.Message);
							}
						}
						#endregion

						#region << task_n_n_bug Relation >>
						{
							var originEntity = entMan.ReadEntity(TASK_ENTITY_ID).Object;
							var targetEntity = entMan.ReadEntity(BUG_ENTITY_ID).Object;
							EntityRelation NToNRelation = new EntityRelation();
							NToNRelation.Id = new Guid("7103355b-bf03-40e1-8446-f6aeecfaa74c");
							NToNRelation.Name = "task_n_n_bug";
							NToNRelation.Label = "task_n_n_bug";
							NToNRelation.System = true;
							NToNRelation.RelationType = EntityRelationType.ManyToMany;
							NToNRelation.OriginEntityId = originEntity.Id;
							NToNRelation.OriginFieldId = originEntity.Fields.Single(x => x.Name == "id").Id;
							NToNRelation.TargetEntityId = targetEntity.Id;
							NToNRelation.TargetFieldId = targetEntity.Fields.Single(x => x.Name == "id").Id;
							{
								var result = relMan.Create(NToNRelation);
								if (!result.Success)
									throw new Exception("CREATE task_n_n_bug RELATION:" + result.Message);
							}
						}
						#endregion

					}
					#endregion

					#region << wv_timelog >>
					{
						#region << entity >>
						{
							InputEntity entity = new InputEntity();
							entity.Id = TIMELOG_ENTITY_ID;
							entity.Name = TIMELOG_ENTITY_NAME;
							entity.Label = "Time log";
							entity.LabelPlural = "Time logs";
							entity.System = true;
							entity.IconName = "clock-o";
							entity.Weight = 9;
							entity.RecordPermissions = new RecordPermissions();
							entity.RecordPermissions.CanCreate = new List<Guid>();
							entity.RecordPermissions.CanRead = new List<Guid>();
							entity.RecordPermissions.CanUpdate = new List<Guid>();
							entity.RecordPermissions.CanDelete = new List<Guid>();
							//Create
							entity.RecordPermissions.CanCreate.Add(SystemIds.AdministratorRoleId);
							entity.RecordPermissions.CanCreate.Add(SystemIds.RegularRoleId);
							//READ
							entity.RecordPermissions.CanRead.Add(SystemIds.AdministratorRoleId);
							entity.RecordPermissions.CanRead.Add(SystemIds.RegularRoleId);
							//UPDATE
							entity.RecordPermissions.CanUpdate.Add(SystemIds.AdministratorRoleId);
							entity.RecordPermissions.CanUpdate.Add(SystemIds.RegularRoleId);
	
							{
								var response = entMan.CreateEntity(entity);
								if (!response.Success)
									throw new Exception("System error 10050. Entity: " + TIMELOG_ENTITY_NAME + " Field: entity creation" + " Message:" + response.Message);
							}
						}
						#endregion

						#region << billable >> 
						{
							InputCheckboxField checkboxField = new InputCheckboxField();
							checkboxField.Id = new Guid("1f4b0729-4e31-4722-a8ce-3bf90c471dad");
							checkboxField.Name = "billable";
							checkboxField.Label = "Billable";
							checkboxField.PlaceholderText = "";
							checkboxField.Description = "";
							checkboxField.HelpText = "";
							checkboxField.Required = true;
							checkboxField.Unique = false;
							checkboxField.Searchable = false;
							checkboxField.Auditable = false;
							checkboxField.System = true;
							checkboxField.DefaultValue = false;
							checkboxField.EnableSecurity = true;
							checkboxField.Permissions = new FieldPermissions();
							checkboxField.Permissions.CanRead = new List<Guid>();
							checkboxField.Permissions.CanUpdate = new List<Guid>();
							//READ
							checkboxField.Permissions.CanRead.Add(SystemIds.AdministratorRoleId);
							checkboxField.Permissions.CanRead.Add(SystemIds.RegularRoleId);
							//UPDATE
							checkboxField.Permissions.CanUpdate.Add(SystemIds.AdministratorRoleId);
							checkboxField.Permissions.CanUpdate.Add(SystemIds.RegularRoleId);
							{
								var response = entMan.CreateField(TIMELOG_ENTITY_ID, checkboxField, false);
								if (!response.Success)
									throw new Exception("System error 10060. Entity: " + TIMELOG_ENTITY_NAME + " Field: field_name" + " Message:" + response.Message);
							}
						}
						#endregion

						#region << hours >>
						{
							InputNumberField numberField = new InputNumberField();
							numberField.Id = new Guid("41caeb03-7430-4eb8-b830-c9df8bf2dc7f");
							numberField.Name = "hours";
							numberField.Label = "Hours";
							numberField.PlaceholderText = "";
							numberField.Description = "";
							numberField.HelpText = "";
							numberField.Required = true;
							numberField.Unique = false;
							numberField.Searchable = false;
							numberField.Auditable = false;
							numberField.System = true;
							numberField.DefaultValue = 0;
							numberField.MinValue = 0;
							numberField.MaxValue = null;
							numberField.DecimalPlaces = 2;
							numberField.EnableSecurity = true;
							numberField.Permissions = new FieldPermissions();
							numberField.Permissions.CanRead = new List<Guid>();
							numberField.Permissions.CanUpdate = new List<Guid>();
							//READ
							numberField.Permissions.CanRead.Add(SystemIds.AdministratorRoleId);
							numberField.Permissions.CanRead.Add(SystemIds.RegularRoleId);
							//UPDATE
							numberField.Permissions.CanUpdate.Add(SystemIds.AdministratorRoleId);
							numberField.Permissions.CanUpdate.Add(SystemIds.RegularRoleId);
							{
								var response = entMan.CreateField(TIMELOG_ENTITY_ID, numberField, false);
								if (!response.Success)
									throw new Exception("System error 10060. Entity: " + TIMELOG_ENTITY_NAME + " Field: hours" + " Message:" + response.Message);
							}
						}
						#endregion

						#region << task_id >>
						{
							InputGuidField guidField = new InputGuidField();
							guidField.Id = new Guid("d256d5f7-4af7-4d62-b265-e4509319d700");
							guidField.Name = "task_id";
							guidField.Label = "Parent task";
							guidField.PlaceholderText = "";
							guidField.Description = "";
							guidField.HelpText = "";
							guidField.Required = true;
							guidField.Unique = false;
							guidField.Searchable = false;
							guidField.Auditable = false;
							guidField.System = true;
							guidField.DefaultValue = Guid.Empty;
							guidField.GenerateNewId = false;
							guidField.EnableSecurity = true;
							guidField.Permissions = new FieldPermissions();
							guidField.Permissions.CanRead = new List<Guid>();
							guidField.Permissions.CanUpdate = new List<Guid>();
							//READ
							guidField.Permissions.CanRead.Add(SystemIds.AdministratorRoleId);
							guidField.Permissions.CanRead.Add(SystemIds.RegularRoleId);
							//UPDATE
							guidField.Permissions.CanUpdate.Add(SystemIds.AdministratorRoleId);
							guidField.Permissions.CanUpdate.Add(SystemIds.RegularRoleId);
							{
								var response = entMan.CreateField(TIMELOG_ENTITY_ID, guidField, false);
								if (!response.Success)
									throw new Exception("System error 10060. Entity: " + TIMELOG_ENTITY_NAME + " Field: task_id" + " Message:" + response.Message);
							}
						}
						#endregion

						#region << bug_id >>
						{
							InputGuidField guidField = new InputGuidField();
							guidField.Id = new Guid("f1b9a8f3-ab8d-4de0-8503-df3165ad7969");
							guidField.Name = "bug_id";
							guidField.Label = "Parent bug";
							guidField.PlaceholderText = "";
							guidField.Description = "";
							guidField.HelpText = "";
							guidField.Required = true;
							guidField.Unique = false;
							guidField.Searchable = false;
							guidField.Auditable = false;
							guidField.System = true;
							guidField.DefaultValue = Guid.Empty;
							guidField.GenerateNewId = false;
							guidField.EnableSecurity = true;
							guidField.Permissions = new FieldPermissions();
							guidField.Permissions.CanRead = new List<Guid>();
							guidField.Permissions.CanUpdate = new List<Guid>();
							//READ
							guidField.Permissions.CanRead.Add(SystemIds.AdministratorRoleId);
							guidField.Permissions.CanRead.Add(SystemIds.RegularRoleId);
							//UPDATE
							guidField.Permissions.CanUpdate.Add(SystemIds.AdministratorRoleId);
							guidField.Permissions.CanUpdate.Add(SystemIds.RegularRoleId);
							{
								var response = entMan.CreateField(TIMELOG_ENTITY_ID, guidField, false);
								if (!response.Success)
									throw new Exception("System error 10060. Entity: " + TIMELOG_ENTITY_NAME + " Field: bug_id" + " Message:" + response.Message);
							}
						}
						#endregion

						#region << task_1_n_time_log Relation >>
						{
							var originEntity = entMan.ReadEntity(TASK_ENTITY_ID).Object;
							var targetEntity = entMan.ReadEntity(TIMELOG_ENTITY_ID).Object;
							EntityRelation oneToNRelation = new EntityRelation();
							oneToNRelation.Id = new Guid("61f1cd54-bcd6-4061-9c96-7934e01f0857");
							oneToNRelation.Name = "task_1_n_time_log";
							oneToNRelation.Label = "task_1_n_time_log";
							oneToNRelation.System = true;
							oneToNRelation.RelationType = EntityRelationType.OneToMany;
							oneToNRelation.OriginEntityId = originEntity.Id;
							oneToNRelation.OriginFieldId = originEntity.Fields.Single(x => x.Name == "id").Id;
							oneToNRelation.TargetEntityId = targetEntity.Id;
							oneToNRelation.TargetFieldId = targetEntity.Fields.Single(x => x.Name == "task_id").Id;
							{
								var result = relMan.Create(oneToNRelation);
								if (!result.Success)
									throw new Exception("CREATE task_1_n_time_log RELATION:" + result.Message);
							}
						}
						#endregion

						#region << bug_1_n_time_log Relation >>
						{
							var originEntity = entMan.ReadEntity(BUG_ENTITY_ID).Object;
							var targetEntity = entMan.ReadEntity(TIMELOG_ENTITY_ID).Object;
							EntityRelation oneToNRelation = new EntityRelation();
							oneToNRelation.Id = new Guid("97909e49-50d4-4534-aa7b-61c523b55d87");
							oneToNRelation.Name = "bug_1_n_time_log";
							oneToNRelation.Label = "bug_1_n_time_log";
							oneToNRelation.System = true;
							oneToNRelation.RelationType = EntityRelationType.OneToMany;
							oneToNRelation.OriginEntityId = originEntity.Id;
							oneToNRelation.OriginFieldId = originEntity.Fields.Single(x => x.Name == "id").Id;
							oneToNRelation.TargetEntityId = targetEntity.Id;
							oneToNRelation.TargetFieldId = targetEntity.Fields.Single(x => x.Name == "bug_id").Id;
							{
								var result = relMan.Create(oneToNRelation);
								if (!result.Success)
									throw new Exception("CREATE bug_1_n_time_log RELATION:" + result.Message);
							}
						}
						#endregion
					}
					#endregion

					#region << wv_project_attachment >>
					{
						#region << entity >>
						{
							InputEntity entity = new InputEntity();
							entity.Id = ATTACHMENT_ENTITY_ID;
							entity.Name = ATTACHMENT_ENTITY_NAME;
							entity.Label = "Attachment";
							entity.LabelPlural = "Attachments";
							entity.System = true;
							entity.IconName = "paperclip";
							entity.Weight = 10;
							entity.RecordPermissions = new RecordPermissions();
							entity.RecordPermissions.CanCreate = new List<Guid>();
							entity.RecordPermissions.CanRead = new List<Guid>();
							entity.RecordPermissions.CanUpdate = new List<Guid>();
							entity.RecordPermissions.CanDelete = new List<Guid>();
							//Create
							entity.RecordPermissions.CanCreate.Add(SystemIds.AdministratorRoleId);
							entity.RecordPermissions.CanCreate.Add(SystemIds.RegularRoleId);
							//READ
							entity.RecordPermissions.CanRead.Add(SystemIds.AdministratorRoleId);
							entity.RecordPermissions.CanRead.Add(SystemIds.RegularRoleId);
							//UPDATE
							entity.RecordPermissions.CanUpdate.Add(SystemIds.AdministratorRoleId);
							entity.RecordPermissions.CanUpdate.Add(SystemIds.RegularRoleId);
							//DELETE
							entity.RecordPermissions.CanDelete.Add(SystemIds.AdministratorRoleId);
							entity.RecordPermissions.CanDelete.Add(SystemIds.RegularRoleId);
		
							{
								var response = entMan.CreateEntity(entity);
								if (!response.Success)
									throw new Exception("System error 10050. Entity: " + ATTACHMENT_ENTITY_NAME + " Field: entity creation" + " Message:" + response.Message);
							}
						}
						#endregion

						#region << file >>
						{
							InputFileField fileField = new InputFileField();
							fileField.Id = new Guid("6d639a8c-e220-4d9f-86f0-de6ba03030b8");
							fileField.Name = "file";
							fileField.Label = "File";
							fileField.PlaceholderText = "";
							fileField.Description = "";
							fileField.HelpText = "";
							fileField.Required = true;
							fileField.Unique = false;
							fileField.Searchable = false;
							fileField.Auditable = false;
							fileField.System = true;
							fileField.DefaultValue = string.Empty;
							fileField.EnableSecurity = true;
							fileField.Permissions = new FieldPermissions();
							fileField.Permissions.CanRead = new List<Guid>();
							fileField.Permissions.CanUpdate = new List<Guid>();
							//READ
							fileField.Permissions.CanRead.Add(SystemIds.AdministratorRoleId);
							fileField.Permissions.CanRead.Add(SystemIds.RegularRoleId);
							//UPDATE
							fileField.Permissions.CanUpdate.Add(SystemIds.AdministratorRoleId);
							fileField.Permissions.CanUpdate.Add(SystemIds.RegularRoleId);
							{
								var response = entMan.CreateField(ATTACHMENT_ENTITY_ID, fileField, false);
								if (!response.Success)
									throw new Exception("System error 10060. Entity: " + ATTACHMENT_ENTITY_NAME + " Field: file" + " Message:" + response.Message);
							}
						}
						#endregion

						#region << task_id >>
						{
							InputGuidField guidField = new InputGuidField();
							guidField.Id = new Guid("841f6741-a7a6-4f52-9b45-31c4bf2e96ae");
							guidField.Name = "task_id";
							guidField.Label = "Parent task";
							guidField.PlaceholderText = "";
							guidField.Description = "";
							guidField.HelpText = "";
							guidField.Required = true;
							guidField.Unique = false;
							guidField.Searchable = false;
							guidField.Auditable = false;
							guidField.System = true;
							guidField.DefaultValue = Guid.Empty;
							guidField.GenerateNewId = false;
							guidField.EnableSecurity = true;
							guidField.Permissions = new FieldPermissions();
							guidField.Permissions.CanRead = new List<Guid>();
							guidField.Permissions.CanUpdate = new List<Guid>();
							//READ
							guidField.Permissions.CanRead.Add(SystemIds.AdministratorRoleId);
							guidField.Permissions.CanRead.Add(SystemIds.RegularRoleId);
							//UPDATE
							guidField.Permissions.CanUpdate.Add(SystemIds.AdministratorRoleId);
							guidField.Permissions.CanUpdate.Add(SystemIds.RegularRoleId);
							{
								var response = entMan.CreateField(ATTACHMENT_ENTITY_ID, guidField, false);
								if (!response.Success)
									throw new Exception("System error 10060. Entity: " + ATTACHMENT_ENTITY_NAME + " Field: task_id" + " Message:" + response.Message);
							}
						}
						#endregion

						#region << bug_id >>
						{
							InputGuidField guidField = new InputGuidField();
							guidField.Id = new Guid("8a389111-cee3-4da0-986f-bcdc5f179924");
							guidField.Name = "bug_id";
							guidField.Label = "Parent bug";
							guidField.PlaceholderText = "";
							guidField.Description = "";
							guidField.HelpText = "";
							guidField.Required = true;
							guidField.Unique = false;
							guidField.Searchable = false;
							guidField.Auditable = false;
							guidField.System = true;
							guidField.DefaultValue = Guid.Empty;
							guidField.GenerateNewId = false;
							guidField.EnableSecurity = true;
							guidField.Permissions = new FieldPermissions();
							guidField.Permissions.CanRead = new List<Guid>();
							guidField.Permissions.CanUpdate = new List<Guid>();
							//READ
							guidField.Permissions.CanRead.Add(SystemIds.AdministratorRoleId);
							guidField.Permissions.CanRead.Add(SystemIds.RegularRoleId);
							//UPDATE
							guidField.Permissions.CanUpdate.Add(SystemIds.AdministratorRoleId);
							guidField.Permissions.CanUpdate.Add(SystemIds.RegularRoleId);
							{
								var response = entMan.CreateField(ATTACHMENT_ENTITY_ID, guidField, false);
								if (!response.Success)
									throw new Exception("System error 10060. Entity: " + ATTACHMENT_ENTITY_NAME + " Field: bug_id" + " Message:" + response.Message);
							}
						}
						#endregion

						#region << task_1_n_attachment Relation >>
						{
							var originEntity = entMan.ReadEntity(TASK_ENTITY_ID).Object;
							var targetEntity = entMan.ReadEntity(ATTACHMENT_ENTITY_ID).Object;
							EntityRelation oneToNRelation = new EntityRelation();
							oneToNRelation.Id = new Guid("f79f76e2-06b1-463a-9675-63845814bf22");
							oneToNRelation.Name = "task_1_n_attachment";
							oneToNRelation.Label = "task_1_n_attachment";
							oneToNRelation.System = true;
							oneToNRelation.RelationType = EntityRelationType.OneToMany;
							oneToNRelation.OriginEntityId = originEntity.Id;
							oneToNRelation.OriginFieldId = originEntity.Fields.Single(x => x.Name == "id").Id;
							oneToNRelation.TargetEntityId = targetEntity.Id;
							oneToNRelation.TargetFieldId = targetEntity.Fields.Single(x => x.Name == "task_id").Id;
							{
								var result = relMan.Create(oneToNRelation);
								if (!result.Success)
									throw new Exception("CREATE task_1_n_attachment RELATION:" + result.Message);
							}
						}
						#endregion

						#region << bug_1_n_attachment Relation >>
						{
							var originEntity = entMan.ReadEntity(BUG_ENTITY_ID).Object;
							var targetEntity = entMan.ReadEntity(ATTACHMENT_ENTITY_ID).Object;
							EntityRelation oneToNRelation = new EntityRelation();
							oneToNRelation.Id = new Guid("a4f60f87-66a9-4541-a2ef-29e00f2b418b");
							oneToNRelation.Name = "bug_1_n_attachment";
							oneToNRelation.Label = "bug_1_n_attachment";
							oneToNRelation.System = true;
							oneToNRelation.RelationType = EntityRelationType.OneToMany;
							oneToNRelation.OriginEntityId = originEntity.Id;
							oneToNRelation.OriginFieldId = originEntity.Fields.Single(x => x.Name == "id").Id;
							oneToNRelation.TargetEntityId = targetEntity.Id;
							oneToNRelation.TargetFieldId = targetEntity.Fields.Single(x => x.Name == "bug_id").Id;
							{
								var result = relMan.Create(oneToNRelation);
								if (!result.Success)
									throw new Exception("CREATE bug_1_n_attachment RELATION:" + result.Message);
							}
						}
						#endregion
					}
					#endregion

					#region << wv_project_activity >>
					{
						#region << entity >>
						{
							InputEntity entity = new InputEntity();
							entity.Id = ACTIVITY_ENTITY_ID;
							entity.Name = ACTIVITY_ENTITY_NAME;
							entity.Label = "Activity";
							entity.LabelPlural = "Activities";
							entity.System = true;
							entity.IconName = "comments-o";
							entity.Weight = 7;
							entity.RecordPermissions = new RecordPermissions();
							entity.RecordPermissions.CanCreate = new List<Guid>();
							entity.RecordPermissions.CanRead = new List<Guid>();
							entity.RecordPermissions.CanUpdate = new List<Guid>();
							entity.RecordPermissions.CanDelete = new List<Guid>();
							//Create
							entity.RecordPermissions.CanCreate.Add(SystemIds.AdministratorRoleId);
							entity.RecordPermissions.CanCreate.Add(SystemIds.RegularRoleId);
							//READ
							entity.RecordPermissions.CanRead.Add(SystemIds.AdministratorRoleId);
							entity.RecordPermissions.CanRead.Add(SystemIds.RegularRoleId);
							//UPDATE
							entity.RecordPermissions.CanUpdate.Add(SystemIds.AdministratorRoleId);
							entity.RecordPermissions.CanUpdate.Add(SystemIds.RegularRoleId);
							{
								var response = entMan.CreateEntity(entity);
								if (!response.Success)
									throw new Exception("System error 10050. Entity: " + ACTIVITY_ENTITY_NAME + " Field: entity creation" + " Message:" + response.Message);
							}
						}
						#endregion

						#region << label >>
						{
							InputSelectField dropdownField = new InputSelectField();
							dropdownField.Id = new Guid("fe4ee5da-8c32-4ecd-8773-04752b413cb0");
							dropdownField.Name = "label";
							dropdownField.Label = "Label";
							dropdownField.PlaceholderText = "";
							dropdownField.Description = "";
							dropdownField.HelpText = "";
							dropdownField.Required = false;
							dropdownField.Unique = false;
							dropdownField.Searchable = false;
							dropdownField.Auditable = false;
							dropdownField.System = true;
							dropdownField.DefaultValue = "added";
							dropdownField.Options = new List<SelectFieldOption>
								{
									new SelectFieldOption(){ Key = "added", Value = "added" },
									new SelectFieldOption(){ Key = "created", Value = "created" },
									new SelectFieldOption(){ Key = "updated", Value = "updated" }
								};
							dropdownField.EnableSecurity = true;
							dropdownField.Permissions = new FieldPermissions();
							dropdownField.Permissions.CanRead = new List<Guid>();
							dropdownField.Permissions.CanUpdate = new List<Guid>();
							//READ
							dropdownField.Permissions.CanRead.Add(SystemIds.AdministratorRoleId);
							dropdownField.Permissions.CanRead.Add(SystemIds.RegularRoleId);
							//UPDATE
							dropdownField.Permissions.CanUpdate.Add(SystemIds.AdministratorRoleId);
							dropdownField.Permissions.CanUpdate.Add(SystemIds.RegularRoleId);
							{
								var response = entMan.CreateField(ACTIVITY_ENTITY_ID, dropdownField, false);
								if (!response.Success)
									throw new Exception("System error 10060. Entity: " + ACTIVITY_ENTITY_NAME + " Field: label" + " Message:" + response.Message);
							}
						}	
						#endregion

						#region << subject >>
						{
							InputTextField textboxField = new InputTextField();
							textboxField.Id = new Guid("8f8b4cb9-aaed-4183-b863-b14faa2496ea");
							textboxField.Name = "subject";
							textboxField.Label = "Subject";
							textboxField.PlaceholderText = "";
							textboxField.Description = "";
							textboxField.HelpText = "";
							textboxField.Required = true;
							textboxField.Unique = false;
							textboxField.Searchable = false;
							textboxField.Auditable = false;
							textboxField.System = true;
							textboxField.DefaultValue = string.Empty;
							textboxField.MaxLength = null;
							textboxField.EnableSecurity = true;
							textboxField.Permissions = new FieldPermissions();
							textboxField.Permissions.CanRead = new List<Guid>();
							textboxField.Permissions.CanUpdate = new List<Guid>();
							//READ
							textboxField.Permissions.CanRead.Add(SystemIds.AdministratorRoleId);
							textboxField.Permissions.CanRead.Add(SystemIds.RegularRoleId);
							//UPDATE
							textboxField.Permissions.CanUpdate.Add(SystemIds.AdministratorRoleId);
							textboxField.Permissions.CanUpdate.Add(SystemIds.RegularRoleId);
							{
								var response = entMan.CreateField(ACTIVITY_ENTITY_ID, textboxField, false);
								if (!response.Success)
									throw new Exception("System error 10060. Entity: " + ACTIVITY_ENTITY_NAME + " Field: subject" + " Message:" + response.Message);
							}
						}
						#endregion

						#region << project_id >>
						{
							InputGuidField guidField = new InputGuidField();
							guidField.Id = new Guid("c67b14eb-e23c-43d1-8fd2-869618390b16");
							guidField.Name = "project_id";
							guidField.Label = "Parent Project";
							guidField.PlaceholderText = "";
							guidField.Description = "";
							guidField.HelpText = "";
							guidField.Required = true;
							guidField.Unique = false;
							guidField.Searchable = false;
							guidField.Auditable = false;
							guidField.System = true;
							guidField.DefaultValue = Guid.Empty;
							guidField.GenerateNewId = false;
							guidField.EnableSecurity = true;
							guidField.Permissions = new FieldPermissions();
							guidField.Permissions.CanRead = new List<Guid>();
							guidField.Permissions.CanUpdate = new List<Guid>();
							//READ
							guidField.Permissions.CanRead.Add(SystemIds.AdministratorRoleId);
							guidField.Permissions.CanRead.Add(SystemIds.RegularRoleId);
							//UPDATE
							guidField.Permissions.CanUpdate.Add(SystemIds.AdministratorRoleId);
							guidField.Permissions.CanUpdate.Add(SystemIds.RegularRoleId);
							{
								var response = entMan.CreateField(ACTIVITY_ENTITY_ID, guidField, false);
								if (!response.Success)
									throw new Exception("System error 10060. Entity: " + ACTIVITY_ENTITY_NAME + " Field: project_id" + " Message:" + response.Message);
							}
						}
						#endregion

						#region << task_id >>
						{
							InputGuidField guidField = new InputGuidField();
							guidField.Id = new Guid("2f919a7c-0167-44bb-a6dc-5a942cc98442");
							guidField.Name = "task_id";
							guidField.Label = "Parent task";
							guidField.PlaceholderText = "";
							guidField.Description = "";
							guidField.HelpText = "";
							guidField.Required = false;
							guidField.Unique = false;
							guidField.Searchable = false;
							guidField.Auditable = false;
							guidField.System = true;
							guidField.DefaultValue = Guid.Empty;
							guidField.GenerateNewId = false;
							guidField.EnableSecurity = true;
							guidField.Permissions = new FieldPermissions();
							guidField.Permissions.CanRead = new List<Guid>();
							guidField.Permissions.CanUpdate = new List<Guid>();
							//READ
							guidField.Permissions.CanRead.Add(SystemIds.AdministratorRoleId);
							guidField.Permissions.CanRead.Add(SystemIds.RegularRoleId);
							//UPDATE
							guidField.Permissions.CanUpdate.Add(SystemIds.AdministratorRoleId);
							guidField.Permissions.CanUpdate.Add(SystemIds.RegularRoleId);
							{
								var response = entMan.CreateField(ACTIVITY_ENTITY_ID, guidField, false);
								if (!response.Success)
									throw new Exception("System error 10060. Entity: " + ACTIVITY_ENTITY_NAME + " Field: task_id" + " Message:" + response.Message);
							}
						}
						#endregion

						#region << bug_id >>
						{
							InputGuidField guidField = new InputGuidField();
							guidField.Id = new Guid("a490f567-a404-4300-be98-e8dde9d3a47e");
							guidField.Name = "bug_id";
							guidField.Label = "Parent bug";
							guidField.PlaceholderText = "";
							guidField.Description = "";
							guidField.HelpText = "";
							guidField.Required = false;
							guidField.Unique = false;
							guidField.Searchable = false;
							guidField.Auditable = false;
							guidField.System = true;
							guidField.DefaultValue = Guid.Empty;
							guidField.GenerateNewId = false;
							guidField.EnableSecurity = true;
							guidField.Permissions = new FieldPermissions();
							guidField.Permissions.CanRead = new List<Guid>();
							guidField.Permissions.CanUpdate = new List<Guid>();
							//READ
							guidField.Permissions.CanRead.Add(SystemIds.AdministratorRoleId);
							guidField.Permissions.CanRead.Add(SystemIds.RegularRoleId);
							//UPDATE
							guidField.Permissions.CanUpdate.Add(SystemIds.AdministratorRoleId);
							guidField.Permissions.CanUpdate.Add(SystemIds.RegularRoleId);
							{
								var response = entMan.CreateField(ACTIVITY_ENTITY_ID, guidField, false);
								if (!response.Success)
									throw new Exception("System error 10060. Entity: " + ACTIVITY_ENTITY_NAME + " Field: bug_id" + " Message:" + response.Message);
							}
						}
						#endregion

						#region << project_1_n_activity Relation >>
						{
							var originEntity = entMan.ReadEntity(PROJECT_ENTITY_ID).Object;
							var targetEntity = entMan.ReadEntity(ACTIVITY_ENTITY_ID).Object;
							EntityRelation oneToNRelation = new EntityRelation();
							oneToNRelation.Id = new Guid("f0894d81-1924-48a8-b8ee-213c90a5f524");
							oneToNRelation.Name = "project_1_n_activity";
							oneToNRelation.Label = "project_1_n_activity";
							oneToNRelation.System = true;
							oneToNRelation.RelationType = EntityRelationType.OneToMany;
							oneToNRelation.OriginEntityId = originEntity.Id;
							oneToNRelation.OriginFieldId = originEntity.Fields.Single(x => x.Name == "id").Id;
							oneToNRelation.TargetEntityId = targetEntity.Id;
							oneToNRelation.TargetFieldId = targetEntity.Fields.Single(x => x.Name == "project_id").Id;
							{
								var result = relMan.Create(oneToNRelation);
								if (!result.Success)
									throw new Exception("CREATE project_1_n_activity RELATION:" + result.Message);
							}
						}
						#endregion

						#region << task_1_n_activity Relation >>
						{
							var originEntity = entMan.ReadEntity(TASK_ENTITY_ID).Object;
							var targetEntity = entMan.ReadEntity(ACTIVITY_ENTITY_ID).Object;
							EntityRelation oneToNRelation = new EntityRelation();
							oneToNRelation.Id = new Guid("8f294277-fd60-496e-bff7-9391fffcda41");
							oneToNRelation.Name = "task_1_n_activity";
							oneToNRelation.Label = "task_1_n_activity";
							oneToNRelation.System = true;
							oneToNRelation.RelationType = EntityRelationType.OneToMany;
							oneToNRelation.OriginEntityId = originEntity.Id;
							oneToNRelation.OriginFieldId = originEntity.Fields.Single(x => x.Name == "id").Id;
							oneToNRelation.TargetEntityId = targetEntity.Id;
							oneToNRelation.TargetFieldId = targetEntity.Fields.Single(x => x.Name == "task_id").Id;
							{
								var result = relMan.Create(oneToNRelation);
								if (!result.Success)
									throw new Exception("CREATE task_1_n_activity RELATION:" + result.Message);
							}
						}
						#endregion

						#region << bug_1_n_activity Relation >>
						{
							var originEntity = entMan.ReadEntity(BUG_ENTITY_ID).Object;
							var targetEntity = entMan.ReadEntity(ACTIVITY_ENTITY_ID).Object;
							EntityRelation oneToNRelation = new EntityRelation();
							oneToNRelation.Id = new Guid("b96189f7-a880-4da4-b9a9-2274a9745d2d");
							oneToNRelation.Name = "bug_1_n_activity";
							oneToNRelation.Label = "bug_1_n_activity";
							oneToNRelation.System = true;
							oneToNRelation.RelationType = EntityRelationType.OneToMany;
							oneToNRelation.OriginEntityId = originEntity.Id;
							oneToNRelation.OriginFieldId = originEntity.Fields.Single(x => x.Name == "id").Id;
							oneToNRelation.TargetEntityId = targetEntity.Id;
							oneToNRelation.TargetFieldId = targetEntity.Fields.Single(x => x.Name == "bug_id").Id;
							{
								var result = relMan.Create(oneToNRelation);
								if (!result.Success)
									throw new Exception("CREATE bug_1_n_activity RELATION:" + result.Message);
							}
						}
						#endregion

					}
					#endregion

					#region << wv_project_comment >>
					{
						#region << entity >>
						{
							InputEntity entity = new InputEntity();
							entity.Id = COMMENT_ENTITY_ID;
							entity.Name = COMMENT_ENTITY_NAME;
							entity.Label = "Comment";
							entity.LabelPlural = "Comments";
							entity.System = true;
							entity.IconName = "comment-o";
							entity.Weight = 17;
							entity.RecordPermissions = new RecordPermissions();
							entity.RecordPermissions.CanCreate = new List<Guid>();
							entity.RecordPermissions.CanRead = new List<Guid>();
							entity.RecordPermissions.CanUpdate = new List<Guid>();
							entity.RecordPermissions.CanDelete = new List<Guid>();
							//Create
							entity.RecordPermissions.CanCreate.Add(SystemIds.AdministratorRoleId);
							entity.RecordPermissions.CanCreate.Add(SystemIds.RegularRoleId);
							//READ
							entity.RecordPermissions.CanRead.Add(SystemIds.AdministratorRoleId);
							entity.RecordPermissions.CanRead.Add(SystemIds.RegularRoleId);
							//UPDATE
							entity.RecordPermissions.CanUpdate.Add(SystemIds.AdministratorRoleId);
							entity.RecordPermissions.CanUpdate.Add(SystemIds.RegularRoleId);
							{
								var response = entMan.CreateEntity(entity);
								if (!response.Success)
									throw new Exception("System error 10050. Entity: " + COMMENT_ENTITY_NAME + " Field: entity creation" + " Message:" + response.Message);
							}
						}
						#endregion

						#region << content >>
						{
							InputHtmlField htmlField = new InputHtmlField();
							htmlField.Id = new Guid("23afb07b-438f-4e31-9372-c850a5789cc6");
							htmlField.Name = "content";
							htmlField.Label = "Content";
							htmlField.PlaceholderText = "";
							htmlField.Description = "";
							htmlField.HelpText = "";
							htmlField.Required = true;
							htmlField.Unique = false;
							htmlField.Searchable = false;
							htmlField.Auditable = false;
							htmlField.System = true;
							htmlField.DefaultValue = string.Empty;
							htmlField.EnableSecurity = true;
							htmlField.Permissions = new FieldPermissions();
							htmlField.Permissions.CanRead = new List<Guid>();
							htmlField.Permissions.CanUpdate = new List<Guid>();
	
							htmlField.Permissions.CanRead.Add(SystemIds.AdministratorRoleId);
							htmlField.Permissions.CanRead.Add(SystemIds.RegularRoleId);
							htmlField.Permissions.CanUpdate.Add(SystemIds.AdministratorRoleId);
							htmlField.Permissions.CanUpdate.Add(SystemIds.RegularRoleId);
							{
								var response = entMan.CreateField(COMMENT_ENTITY_ID, htmlField, false);
								if (!response.Success)
									throw new Exception("System error 10060. Entity: " + COMMENT_ENTITY_ID + " Field: field_name" + " Message:" + response.Message);
							}
						}
						#endregion

						#region << task_id >>
						{
							InputGuidField guidField = new InputGuidField();
							guidField.Id = new Guid("40068814-738f-4725-8aef-4722bc37ee7d");
							guidField.Name = "task_id";
							guidField.Label = "Parent task";
							guidField.PlaceholderText = "";
							guidField.Description = "";
							guidField.HelpText = "";
							guidField.Required = false;
							guidField.Unique = false;
							guidField.Searchable = false;
							guidField.Auditable = false;
							guidField.System = true;
							guidField.DefaultValue = Guid.Empty;
							guidField.GenerateNewId = false;
							guidField.EnableSecurity = true;
							guidField.Permissions = new FieldPermissions();
							guidField.Permissions.CanRead = new List<Guid>();
							guidField.Permissions.CanUpdate = new List<Guid>();
							//READ
							guidField.Permissions.CanRead.Add(SystemIds.AdministratorRoleId);
							guidField.Permissions.CanRead.Add(SystemIds.RegularRoleId);
							//UPDATE
							guidField.Permissions.CanUpdate.Add(SystemIds.AdministratorRoleId);
							guidField.Permissions.CanUpdate.Add(SystemIds.RegularRoleId);
							{
								var response = entMan.CreateField(COMMENT_ENTITY_ID, guidField, false);
								if (!response.Success)
									throw new Exception("System error 10060. Entity: " + COMMENT_ENTITY_NAME + " Field: task_id" + " Message:" + response.Message);
							}
						}
						#endregion

						#region << bug_id >>
						{
							InputGuidField guidField = new InputGuidField();
							guidField.Id = new Guid("6ac276d0-bb0c-4f8f-b30d-ea373cb64b73");
							guidField.Name = "bug_id";
							guidField.Label = "Parent bug";
							guidField.PlaceholderText = "";
							guidField.Description = "";
							guidField.HelpText = "";
							guidField.Required = false;
							guidField.Unique = false;
							guidField.Searchable = false;
							guidField.Auditable = false;
							guidField.System = true;
							guidField.DefaultValue = Guid.Empty;
							guidField.GenerateNewId = false;
							guidField.EnableSecurity = true;
							guidField.Permissions = new FieldPermissions();
							guidField.Permissions.CanRead = new List<Guid>();
							guidField.Permissions.CanUpdate = new List<Guid>();
							//READ
							guidField.Permissions.CanRead.Add(SystemIds.AdministratorRoleId);
							guidField.Permissions.CanRead.Add(SystemIds.RegularRoleId);
							//UPDATE
							guidField.Permissions.CanUpdate.Add(SystemIds.AdministratorRoleId);
							guidField.Permissions.CanUpdate.Add(SystemIds.RegularRoleId);
							{
								var response = entMan.CreateField(COMMENT_ENTITY_ID, guidField, false);
								if (!response.Success)
									throw new Exception("System error 10060. Entity: " + COMMENT_ENTITY_NAME + " Field: bug_id" + " Message:" + response.Message);
							}
						}
						#endregion

						#region << task_1_n_comment Relation >>
						{
							var originEntity = entMan.ReadEntity(TASK_ENTITY_ID).Object;
							var targetEntity = entMan.ReadEntity(COMMENT_ENTITY_ID).Object;
							EntityRelation oneToNRelation = new EntityRelation();
							oneToNRelation.Id = new Guid("884b9480-dc1c-468a-98f0-2d5f10084622");
							oneToNRelation.Name = "task_1_n_comment";
							oneToNRelation.Label = "task_1_n_comment";
							oneToNRelation.System = true;
							oneToNRelation.RelationType = EntityRelationType.OneToMany;
							oneToNRelation.OriginEntityId = originEntity.Id;
							oneToNRelation.OriginFieldId = originEntity.Fields.Single(x => x.Name == "id").Id;
							oneToNRelation.TargetEntityId = targetEntity.Id;
							oneToNRelation.TargetFieldId = targetEntity.Fields.Single(x => x.Name == "task_id").Id;
							{
								var result = relMan.Create(oneToNRelation);
								if (!result.Success)
									throw new Exception("CREATE task_1_n_comment RELATION:" + result.Message);
							}
						}
						#endregion

						#region << bug_1_n_comment Relation >>
						{
							var originEntity = entMan.ReadEntity(BUG_ENTITY_ID).Object;
							var targetEntity = entMan.ReadEntity(COMMENT_ENTITY_ID).Object;
							EntityRelation oneToNRelation = new EntityRelation();
							oneToNRelation.Id = new Guid("5af026bd-d046-42ba-b6a0-e9090727348f");
							oneToNRelation.Name = "bug_1_n_comment";
							oneToNRelation.Label = "bug_1_n_comment";
							oneToNRelation.System = true;
							oneToNRelation.RelationType = EntityRelationType.OneToMany;
							oneToNRelation.OriginEntityId = originEntity.Id;
							oneToNRelation.OriginFieldId = originEntity.Fields.Single(x => x.Name == "id").Id;
							oneToNRelation.TargetEntityId = targetEntity.Id;
							oneToNRelation.TargetFieldId = targetEntity.Fields.Single(x => x.Name == "bug_id").Id;
							{
								var result = relMan.Create(oneToNRelation);
								if (!result.Success)
									throw new Exception("CREATE bug_1_n_comment RELATION:" + result.Message);
							}
						}
						#endregion

					}
					#endregion

				}
				#endregion

				#region << 4. Save needed changes to the plugin setting data >>
				{
					string json = JsonConvert.SerializeObject(currentPluginSettings);
					var settingsEntityRecord = new EntityRecord();
					settingsEntityRecord["id"] = WEBVELLA_PROJECT_PLUGIN_ID;
					settingsEntityRecord["name"] = WEBVELLA_PROJECT_PLUGIN_NAME;
					settingsEntityRecord["data"] = json;
					var settingsUpdateReponse = recMan.UpdateRecord("plugin_data",settingsEntityRecord);
					if (!settingsUpdateReponse.Success)
						throw new Exception("plugin 'webvella-project' failed to update its settings in the database due to: " + pluginDataQueryResponse.Message);
				}
				#endregion
			}
		}
	}
}
