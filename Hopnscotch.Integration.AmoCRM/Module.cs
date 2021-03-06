﻿using Autofac;
using Hopnscotch.Portal.Integration.AmoCRM.DataProvider;

namespace Hopnscotch.Portal.Integration.AmoCRM
{
    public class Module : Autofac.Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<AmoDataProvider>().As<IAmoDataProvider>();
        }
    }
}
