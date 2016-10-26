﻿//  Copyright 2016 Google Inc. All Rights Reserved.
//
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//
//  http ://www.apache.org/licenses/LICENSE-2.0
//
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.

using System;
using System.Collections.Generic;

namespace NtApiDotNet
{
    /// <summary>
    /// Class to create a new user process using the native APIs.
    /// </summary>
    public sealed class CreateUserProcess
    {
        /// <summary>
        /// Path to the executable to start.
        /// </summary>
        public string ImagePath { get; set; }

        /// <summary>
        /// Path to the executable to start which is passed in the process configuration.
        /// </summary>
        public string ConfigImagePath
        {
            get; set;
        }

        /// <summary>
        /// Command line
        /// </summary>
        public string CommandLine { get; set; }

        /// <summary>
        /// Prepared environment block.
        /// </summary>
        public byte[] Environment
        {
            get; set;
        }

        /// <summary>
        /// Title of the main window.
        /// </summary>
        public string WindowTitle { get; set; }

        /// <summary>
        /// Path to DLLs.
        /// </summary>
        public string DllPath { get; set; }
        public string CurrentDirectory { get; set; }
        public string DesktopInfo { get; set; }
        public string ShellInfo
        {
            get; set;
        }

        public string RuntimeData
        {
            get; set;
        }

        public ImageCharacteristics ProhibitedImageCharacteristics
        {
            get; set;
        }

        public FileAccessRights AdditionalFileAccess
        {
            get; set;
        }

        public ProcessCreateFlags ProcessFlags
        {
            get; set;
        }

        public ThreadCreateFlags ThreadFlags
        {
            get; set;
        }

        public ProcessCreateInitFlag InitFlags
        {
            get; set;
        }

        public bool RestrictChildProcess
        {
            get; set;
        }

        public bool OverrideRestrictChildProcess
        {
            get; set;
        }

        public List<ProcessAttribute> AdditionalAttributes
        {
            get; private set;
        }

        public bool ReturnOnError
        {
            get; set;
        }

        public CreateUserProcess()
        {
            DesktopInfo = @"WinSta0\Default";
            ShellInfo = "";
            RuntimeData = "";
            WindowTitle = "";
            AdditionalAttributes = new List<ProcessAttribute>();
        }

        public static CreateUserProcessResult Fork()
        {
            List<ProcessAttribute> attrs = new List<ProcessAttribute>();
            try
            {
                ProcessCreateInfo create_info = new ProcessCreateInfo();
                SafeKernelObjectHandle process_handle;
                SafeKernelObjectHandle thread_handle;

                SafeStructureInOutBuffer<ClientId> client_id = new SafeStructureInOutBuffer<ClientId>();
                attrs.Add(ProcessAttribute.ClientId(client_id));

                ProcessAttributeList attr_list = new ProcessAttributeList(attrs);

                NtStatus status = NtSystemCalls.NtCreateUserProcess(
                  out process_handle, out thread_handle,
                  ProcessAccessRights.MaximumAllowed, ThreadAccessRights.MaximumAllowed,
                  null, null, ProcessCreateFlags.InheritFromParent,
                  ThreadCreateFlags.Suspended, IntPtr.Zero, create_info, attr_list);

                NtObject.StatusToNtException(status);

                return new CreateUserProcessResult(process_handle, thread_handle,
                  create_info.Data, new SectionImageInformation(), client_id.Result);
            }
            finally
            {
                foreach (ProcessAttribute attr in attrs)
                {
                    attr.Dispose();
                }
            }
        }

        public CreateUserProcessResult Start(string image_path)
        {
            if (image_path == null)
                throw new System.ArgumentNullException("image_path");

            IntPtr process_params = NtProcess.CreateProcessParameters(ImagePath ?? image_path, DllPath, CurrentDirectory,
                  CommandLine, Environment, WindowTitle, DesktopInfo, ShellInfo, RuntimeData, 1);
            List<ProcessAttribute> attrs = new List<ProcessAttribute>();
            try
            {
                ProcessCreateInfo create_info = new ProcessCreateInfo();
                SafeKernelObjectHandle process_handle;
                SafeKernelObjectHandle thread_handle;

                attrs.Add(ProcessAttribute.ImageName(image_path));
                SafeStructureInOutBuffer<SectionImageInformation> image_info = new SafeStructureInOutBuffer<SectionImageInformation>();
                attrs.Add(ProcessAttribute.ImageInfo(image_info));
                SafeStructureInOutBuffer<ClientId> client_id = new SafeStructureInOutBuffer<ClientId>();
                attrs.Add(ProcessAttribute.ClientId(client_id));
                attrs.AddRange(AdditionalAttributes);

                if (RestrictChildProcess || OverrideRestrictChildProcess)
                {
                    attrs.Add(ProcessAttribute.ChildProcess(RestrictChildProcess, OverrideRestrictChildProcess));
                }

                ProcessAttributeList attr_list = new ProcessAttributeList(attrs);

                create_info.Data.InitFlags = InitFlags | ProcessCreateInitFlag.WriteOutputOnExit;
                create_info.Data.ProhibitedImageCharacteristics = ProhibitedImageCharacteristics;
                create_info.Data.AdditionalFileAccess = AdditionalFileAccess;

                NtStatus status = NtSystemCalls.NtCreateUserProcess(
                  out process_handle, out thread_handle,
                  ProcessAccessRights.MaximumAllowed, ThreadAccessRights.MaximumAllowed,
                  null, null, ProcessFlags,
                  ThreadFlags, process_params, create_info, attr_list);

                if ((int)status < 0 && !ReturnOnError)
                {
                    // Close handles which come from errors
                    switch (create_info.State)
                    {
                        case ProcessCreateState.FailOnSectionCreate:
                            NtSystemCalls.NtClose(create_info.Data.FileHandle);
                            break;
                        case ProcessCreateState.FailExeName:
                            NtSystemCalls.NtClose(create_info.Data.IFEOKey);
                            break;
                    }

                    NtObject.StatusToNtException(status);
                }

                if (create_info.State == ProcessCreateState.Success)
                {
                    return new CreateUserProcessResult(process_handle, thread_handle,
                      create_info.Data, image_info.Result, client_id.Result);
                }
                else
                {
                    return new CreateUserProcessResult(status, create_info.Data, create_info.State);
                }
            }
            finally
            {
                NtSystemCalls.RtlDestroyProcessParameters(process_params);
                foreach (ProcessAttribute attr in attrs)
                {
                    attr.Dispose();
                }
            }
        }
    }
}
