﻿using Microsoft.Extensions.Logging;
using System;

namespace Common
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
