﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.34209
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace SSSB {
    using System;
    
    
    /// <summary>
    ///   A strongly-typed resource class, for looking up localized strings, etc.
    /// </summary>
    // This class was auto-generated by the StronglyTypedResourceBuilder
    // class via a tool like ResGen or Visual Studio.
    // To add or remove a member, edit your .ResX file then rerun ResGen
    // with the /str option, or rebuild your VS project.
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "4.0.0.0")]
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    internal class ServiceBrokerResources {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal ServiceBrokerResources() {
        }
        
        /// <summary>
        ///   Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("SSSB.SSSB.ServiceBrokerResources", typeof(ServiceBrokerResources).Assembly);
                    resourceMan = temp;
                }
                return resourceMan;
            }
        }
        
        /// <summary>
        ///   Overrides the current thread's CurrentUICulture property for all
        ///   resource lookups using this strongly typed resource class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Globalization.CultureInfo Culture {
            get {
                return resourceCulture;
            }
            set {
                resourceCulture = value;
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Ошибка отправки сообщений пробуждения потоков службы.
        /// </summary>
        internal static string AwakeServiceThreadsErrMsg {
            get {
                return ResourceManager.GetString("AwakeServiceThreadsErrMsg", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Ошибка начала диалога обмена сообщениями.
        /// </summary>
        internal static string BeginDialogConversationErrMsg {
            get {
                return ResourceManager.GetString("BeginDialogConversationErrMsg", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Ошибка завершения диалога обмена сообщениями.
        /// </summary>
        internal static string EndConversationErrMsg {
            get {
                return ResourceManager.GetString("EndConversationErrMsg", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Диалог {0} завершен с ошибкой {1}.
        /// </summary>
        internal static string ErrorMessageReceivedErrMsg {
            get {
                return ResourceManager.GetString("ErrorMessageReceivedErrMsg", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Ошибка получения названия очереди сообщений.
        /// </summary>
        internal static string GetServiceQueueNameErrMsg {
            get {
                return ResourceManager.GetString("GetServiceQueueNameErrMsg", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Ошибка добавления сообщения в очередь к обработке по дате активации.
        /// </summary>
        internal static string PendingMessageErrMsg {
            get {
                return ResourceManager.GetString("PendingMessageErrMsg", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Ошибка обработки сообщений.
        /// </summary>
        internal static string ProcessMessagesErrMsg {
            get {
                return ResourceManager.GetString("ProcessMessagesErrMsg", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Ошибка отправки сообщения.
        /// </summary>
        internal static string SendMessageErrMsg {
            get {
                return ResourceManager.GetString("SendMessageErrMsg", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Ошибка инициализации сервиса {0}. В базе данных отсутствует сервис с указанным именем..
        /// </summary>
        internal static string ServiceInitializationErrMsg {
            get {
                return ResourceManager.GetString("ServiceInitializationErrMsg", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Ошибка запуска сервиса обработки сообщений.
        /// </summary>
        internal static string StartErrMsg {
            get {
                return ResourceManager.GetString("StartErrMsg", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Ошибка остановки сервиса обработки сообщений.
        /// </summary>
        internal static string StopErrMsg {
            get {
                return ResourceManager.GetString("StopErrMsg", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Неизвестный тип сообщения {0}.
        /// </summary>
        internal static string UnknownMessageTypeErrMsg {
            get {
                return ResourceManager.GetString("UnknownMessageTypeErrMsg", resourceCulture);
            }
        }
    }
}
