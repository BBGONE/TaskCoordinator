﻿using Microsoft.Extensions.Logging;
using System;

namespace Shared
{
    public static class LogFactory
    {
        public static ILoggerFactory Instance
        {
            get { return loggerFactory.Value; }
        }
        
        private static Lazy<ILoggerFactory> loggerFactory = new Lazy<ILoggerFactory>(() => new LoggerFactory(), true);
    }
}
