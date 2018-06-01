using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Autofac;
using AutoMapper;

namespace ArchitectNow.AutoMapper.Autofac
{
    public static class ContainerBuilderExtensions
    {
        public static ContainerBuilder AddAutoMapper(this ContainerBuilder containerBuilder)
        {
            return containerBuilder.AddAutoMapper(null, AppDomain.CurrentDomain.GetAssemblies());
        }

        public static ContainerBuilder AddAutoMapper(this ContainerBuilder containerBuilder, Action<IMapperConfigurationExpression> additionalInitAction)
        {
            return containerBuilder.AddAutoMapper(additionalInitAction, AppDomain.CurrentDomain.GetAssemblies());
        }

        public static ContainerBuilder AddAutoMapper(this ContainerBuilder containerBuilder, Action<IMapperConfigurationExpression> additionalInitAction, ResolutionContext dependencyContext)
        {
            return containerBuilder.AddAutoMapper(additionalInitAction, AppDomain.CurrentDomain.GetAssemblies());
        }

        private static readonly Action<IMapperConfigurationExpression> DefaultConfig = cfg => { };

        public static ContainerBuilder AddAutoMapper(this ContainerBuilder containerBuilder, params Assembly[] assemblies)
            => AddAutoMapperClasses(containerBuilder, null, assemblies);

        public static ContainerBuilder AddAutoMapper(this ContainerBuilder containerBuilder, Action<IMapperConfigurationExpression> additionalInitAction, params Assembly[] assemblies) 
            => AddAutoMapperClasses(containerBuilder, additionalInitAction, assemblies);

        public static ContainerBuilder AddAutoMapper(this ContainerBuilder containerBuilder, Action<IMapperConfigurationExpression> additionalInitAction, IEnumerable<Assembly> assemblies) 
            => AddAutoMapperClasses(containerBuilder, additionalInitAction, assemblies);

        public static ContainerBuilder AddAutoMapper(this ContainerBuilder containerBuilder, IEnumerable<Assembly> assemblies)
            => AddAutoMapperClasses(containerBuilder, null, assemblies);

        public static ContainerBuilder AddAutoMapper(this ContainerBuilder containerBuilder, params Type[] profileAssemblyMarkerTypes)
        {
            return AddAutoMapperClasses(containerBuilder, null, profileAssemblyMarkerTypes.Select(t => IntrospectionExtensions.GetTypeInfo(t).Assembly));
        }

        public static ContainerBuilder AddAutoMapper(this ContainerBuilder containerBuilder, Action<IMapperConfigurationExpression> additionalInitAction, params Type[] profileAssemblyMarkerTypes)
        {
            return AddAutoMapperClasses(containerBuilder, additionalInitAction, profileAssemblyMarkerTypes.Select(t => t.GetTypeInfo().Assembly));
        }

        public static ContainerBuilder AddAutoMapper(this ContainerBuilder containerBuilder, Action<IMapperConfigurationExpression> additionalInitAction, IEnumerable<Type> profileAssemblyMarkerTypes)
        {
            return AddAutoMapperClasses(containerBuilder, additionalInitAction, profileAssemblyMarkerTypes.Select(t => t.GetTypeInfo().Assembly));
        }


        private static ContainerBuilder AddAutoMapperClasses(ContainerBuilder containerBuilder, Action<IMapperConfigurationExpression> additionalInitAction, IEnumerable<Assembly> assembliesToScan)
        {
            additionalInitAction = additionalInitAction ?? DefaultConfig;
            assembliesToScan = assembliesToScan as Assembly[] ?? assembliesToScan.ToArray();
            
            var allTypes = assembliesToScan
                .Where(a => a.GetName().Name != nameof(AutoMapper))
                .SelectMany(a => a.DefinedTypes)
                .ToArray();

            var profiles = allTypes
                .Where(t => typeof(Profile).GetTypeInfo().IsAssignableFrom(t) && !t.IsAbstract)
                .Select(t=> t.AsType())
                .ToArray();

            containerBuilder.RegisterTypes(profiles).As<Profile>();

            containerBuilder.Register(context =>
            {
                var config = new MapperConfiguration(expression =>
                {
                    foreach (var profile in context.Resolve<IEnumerable<Profile>>())
                    {
                        expression.AddProfile(profile);
                    }
                    additionalInitAction?.Invoke(expression);
                });
                return config;
            }).As<IConfigurationProvider>().SingleInstance();

            var openTypes = new[]
            {
                typeof(IValueResolver<,,>),
                typeof(IMemberValueResolver<,,,>),
                typeof(ITypeConverter<,>),
                typeof(IMappingAction<,>)
            };
            foreach (var type in openTypes.SelectMany(openType => allTypes
                .Where(t => t.IsClass 
                            && !t.IsAbstract 
                            && t.AsType().ImplementsGenericInterface(openType))))
            {
                containerBuilder.RegisterType(type.AsType());
            }
            
            containerBuilder.Register(sp => new Mapper(sp.Resolve<IConfigurationProvider>(), sp.Resolve)).As<IMapper>().InstancePerLifetimeScope().AutoActivate();

            return containerBuilder;
        }

        private static bool ImplementsGenericInterface(this Type type, Type interfaceType)
        {
            return type.IsGenericType(interfaceType) || type.GetTypeInfo().ImplementedInterfaces.Any(@interface => @interface.IsGenericType(interfaceType));
        }

        private static bool IsGenericType(this Type type, Type genericType)
            => type.GetTypeInfo().IsGenericType && type.GetGenericTypeDefinition() == genericType;
    }
}