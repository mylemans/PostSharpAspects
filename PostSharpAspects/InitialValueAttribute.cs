using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using Mylemans.PostSharp.Utilities;
using PostSharp.Aspects;
using PostSharp.Aspects.Dependencies;
using PostSharp.Reflection;

namespace Mylemans.PostSharp.Aspects
{
	///<summary>
	/// Use this InitialValue aspect to define a 'default' value on properties (and fields, if you wish so)
	/// 
	/// Be aware that the initial value will only be generated if you 'get' the value, before you 'set' it.
	/// 
	/// Also, before returning the defined initial value, it'll be stored inside the field or property (causing a 'set' operation) 
	/// 
	/// This aspect can be used to take away the need to initialize properties from a constructor.
	///</summary>
	[Serializable]
	[
		ProvideAspectRole(AspectRoles.ProvidesInitialValue),
		AspectRoleDependency(AspectDependencyAction.Conflict, AspectRoles.ProvidesInitialValue)
	]
	public sealed class InitialValueAttribute : LocationInterceptionAspect, IInstanceScopedAspect, IAspectProvider
	{
		private bool _isInitialized;
		private readonly object _value;
		private readonly bool _hasValue = true;
		private readonly Type _valueType;
		private readonly string _valueString;

		/// <summary>
		/// When set to true, will attach a DefaultValueAttribute automaticly with the same value. 
		/// 
		/// A visual designer can use the default value to reset the member's value. 
		/// Code generators can use the default values also to determine whether code should be generated for the member.
		/// 
		/// When a DefaultValueAttribute is attached already, no new one will be attached.
		/// 
		/// (default: false)
		/// </summary>
		public bool AttachDefaultValue { get; set; }

		///<summary>
		/// Returns the value (or create a 'new' value if it required a converter)
		///</summary>
		public object Value
		{
			get
			{
				return (_valueType == null) ? _value : TypeDescriptor.GetConverter(_valueType).ConvertFromInvariantString(_valueString);
			}
		}

		/// <summary>
		/// 
		/// </summary>
		public InitialValueAttribute(Type type, string value)
		{
			try
			{
				_valueString = value;
				_valueType = type;

				_value = TypeDescriptor.GetConverter(type).ConvertFromInvariantString(value);
			}
			catch
			{
				_hasValue = false;
			}
		}

		/// <summary>
		/// The initial value is a char
		/// </summary>
		public InitialValueAttribute(char value)
		{
			_value = value;
		}

		/// <summary>
		/// The initial value is a byte
		/// </summary>
		public InitialValueAttribute(byte value)
		{
			_value = value;
		}

		/// <summary>
		/// The initial value is a short
		/// </summary>
		public InitialValueAttribute(short value)
		{
			_value = value;
		}

		/// <summary>
		/// The initial value is an int
		/// </summary>
		public InitialValueAttribute(int value)
		{
			_value = value;
		}

		/// <summary>
		/// The initial value is a long
		/// </summary>
		public InitialValueAttribute(long value)
		{
			_value = value;
		}

		/// <summary>
		/// The initial value is a float
		/// </summary>
		public InitialValueAttribute(float value)
		{
			_value = value;
		}

		/// <summary>
		/// The initial value is a double
		/// </summary>
		public InitialValueAttribute(double value)
		{
			_value = value;
		}

		/// <summary>
		/// The initial value is a boolean
		/// </summary>
		public InitialValueAttribute(bool value)
		{
			_value = value;
		}

		/// <summary>
		/// The initial value is a string
		/// </summary>
		public InitialValueAttribute(string value)
		{
			_value = value;
		}

		/// <summary>
		/// The initial value is of an unknown type
		/// </summary>
		public InitialValueAttribute(object value)
		{
			_value = value;
		}

		#region Implementation of IInstanceScopedAspect
		/// <summary>
		/// Create a clone of this aspect instance
		/// </summary>
		public object CreateInstance(AdviceArgs adviceArgs)
		{
			return MemberwiseClone();
		}

		/// <summary>
		/// Initialize the instance
		/// </summary>
		public void RuntimeInitializeInstance()
		{

		}
		#endregion

		/// <summary>
		/// Called on 'get' of the field or property, when it is the first 'get', and there was no 'set' before this get,
		/// we will return 'our' value
		/// </summary>
		public override void OnGetValue(LocationInterceptionArgs args)
		{
			if (_isInitialized)
			{
				args.ProceedGetValue();
				return;
			}

			args.SetNewValue(Value); // store the value using the property its setter
			args.ProceedGetValue(); // then proceed the 'get' as usual
		}

		/// <summary>
		/// Called when the field or property is 'set'
		/// </summary>
		public override void OnSetValue(LocationInterceptionArgs args)
		{
			// If it was not initialized yet, mark it as such now
			if (!_isInitialized)
			{
				_isInitialized = true;
			}

			args.ProceedSetValue(); // Proceed with the set
		}

		/// <summary>
		/// Perform compile-time validation
		/// </summary>
		public override bool CompileTimeValidate(LocationInfo locationInfo)
		{
			if (!_hasValue)
			{
				return this.RaiseError(1, "The specified type '{0}' does not have a converter that can parse: {1}".F(_valueType.FullName, _valueString), locationInfo.AsSignature());
			}

			if (locationInfo.FieldInfo == null && locationInfo.PropertyInfo.GetSetMethod(true) == null)
			{
				return this.RaiseError(2, "The aspect can only be applied to a property if it has a setter.", locationInfo.AsSignature());
			}


			if (_value == null)
			{
				this.RaiseWarning(3, "Not applying the aspect as it would return a 'null' on initialization.", locationInfo.AsSignature());

				return false;
			}

			if (locationInfo.FieldInfo == null)
			{
				this.Describe("When the property is 'get' for the first time, the defined default value will be created, set using the 'setter' and then returned, but only if there was nothing 'set' before.", locationInfo);
			}
			else
			{
				this.Describe("When the field is 'get' for the first time, the defined default value will be created, stored and returned, but only if there was nothing 'set' before.", locationInfo);
			}

			return base.CompileTimeValidate(locationInfo);
		}

		#region Implementation of IAspectProvider


		[NonSerialized]
		private CustomAttributeIntroductionAspect _defaultValueAttributeIntroduction;

		/// <summary>
		/// This aspect also can attach DefaultValueAttribute to the marked elements (field / property)
		/// 
		/// You need to set 'AttachDefaultValue' to 'true' to use this.
		/// 
		/// No new DefaultValueAttribute will be attached if there is already one attached
		/// </summary>
		public IEnumerable<AspectInstance> ProvideAspects(object targetElement)
		{
			// if we do not have a value, post-compile checks failed
			if (!_hasValue)
			{
				yield break;
				
			}

			// define the attribute (if it wasn't defined before)
			if (_defaultValueAttributeIntroduction == null)
			{
				if (_valueType == null)
				{
					var constructorType = (_value != null) ? _value.GetType() : typeof(object);

					_defaultValueAttributeIntroduction =
						new CustomAttributeIntroductionAspect(
							new ObjectConstruction(typeof(DefaultValueAttribute).GetConstructor(new[] { constructorType }), _value));
				}
				else
				{
					_defaultValueAttributeIntroduction =
						new CustomAttributeIntroductionAspect(
							new ObjectConstruction(typeof(DefaultValueAttribute).GetConstructor(new[] { typeof(Type), typeof(string) }), _valueType, _valueString));

				}
			}

			var fieldInfo = targetElement as FieldInfo;
			var propertyInfo = targetElement as PropertyInfo;
			var locationInfo = targetElement as LocationInfo;

			if (locationInfo != null)
			{
				fieldInfo = locationInfo.FieldInfo;
				propertyInfo = locationInfo.PropertyInfo;
			}

			if (propertyInfo != null)
			{
				if (!propertyInfo.IsDefined(typeof(DefaultValueAttribute), false))
				{
					yield return new AspectInstance(propertyInfo, _defaultValueAttributeIntroduction);
				}
			}

			if (fieldInfo != null)
			{
				if (!fieldInfo.IsDefined(typeof(DefaultValueAttribute), false))
				{
					yield return new AspectInstance(fieldInfo, _defaultValueAttributeIntroduction);
				}
			}

			yield break;
		}

		#endregion
	}
}
