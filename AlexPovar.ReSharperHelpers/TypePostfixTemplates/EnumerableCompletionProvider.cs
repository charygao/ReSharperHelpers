using System.Collections.Generic;
using JetBrains.Annotations;
using JetBrains.ReSharper.Feature.Services.CodeCompletion;
using JetBrains.ReSharper.Feature.Services.CodeCompletion.Infrastructure.AspectLookupItems.BaseInfrastructure;
using JetBrains.ReSharper.Feature.Services.CodeCompletion.Infrastructure.AspectLookupItems.Info;
using JetBrains.ReSharper.Feature.Services.CodeCompletion.Infrastructure.AspectLookupItems.Presentations;
using JetBrains.ReSharper.Feature.Services.CodeCompletion.Infrastructure.LookupItems;
using JetBrains.ReSharper.Feature.Services.CSharp.CodeCompletion.Infrastructure;
using JetBrains.ReSharper.Feature.Services.Resources;
using JetBrains.ReSharper.Features.Intellisense.CodeCompletion.CSharp;
using JetBrains.ReSharper.Features.Intellisense.CodeCompletion.CSharp.Rules;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.CSharp;
using JetBrains.ReSharper.Psi.CSharp.Parsing;
using JetBrains.ReSharper.Psi.CSharp.Tree;
using JetBrains.ReSharper.Psi.Resolve;
using JetBrains.ReSharper.Psi.Tree;
using JetBrains.UI.Icons;
using JetBrains.Util;

namespace AlexPovar.ReSharperHelpers.TypePostfixTemplates
{
  [Language(typeof(CSharpLanguage))]
  public class EnumerableCompletionProvider : CSharpItemsProviderBase<CSharpCodeCompletionContext>
  {
    protected override bool AddLookupItems(CSharpCodeCompletionContext context, GroupedItemsCollector collector)
    {
      var currentType = this.TryResolveCurrentType(context);
      if (currentType == null) return false;

      var factory = CSharpLookupItemFactory.Instance;

      foreach (var completionType in this.CreateCompletionTypes(currentType, context))
      {
        var lookupItem = factory.CreateTypeLookupItem(context, completionType)
          .WithPresentation(itemInfo => new ContainerPostfixPresentation(itemInfo.Info));

        collector.Add(lookupItem);
      }

      return true;
    }

    protected override bool IsAvailable(CSharpCodeCompletionContext context)
    {
      return context.BasicContext.CodeCompletionType == CodeCompletionType.BasicCompletion;
    }

    [CanBeNull]
    private IDeclaredType ResolveTypeFromReference([CanBeNull] IReference reference)
    {
      var resolveResult = reference?.Resolve().Result;
      var typeElement = resolveResult?.DeclaredElement as ITypeElement;

      if (typeElement == null) return null;
      return TypeFactory.CreateType(typeElement, resolveResult.Substitution);
    }

    [CanBeNull]
    private IDeclaredType TryResolveCurrentType([NotNull] CSharpCodeCompletionContext context)
    {
      var completionContext = context.UnterminatedContext ?? context.TerminatedContext;
      var identifier = completionContext?.TreeNode as ICSharpIdentifier;

      if (identifier == null) return null;

      var asReferenceName = identifier.Parent as IReferenceName;
      if (asReferenceName != null)
      {
        var typeUsage = asReferenceName.Parent as ITypeUsage;
        if (typeUsage != null)
        {
          return this.ResolveTypeFromReference(asReferenceName.Qualifier?.Reference);
        }

        return null;
      }

      var asRefExpression = identifier.Parent as IReferenceExpression;
      if (asRefExpression != null)
      {
        //Handle custom types.
        var refQualifierExpr = asRefExpression.QualifierExpression as IReferenceExpression;
        if (refQualifierExpr != null)
        {
          return this.ResolveTypeFromReference(refQualifierExpr.Reference);
        }

        var predefinedTypeExpr = asRefExpression.QualifierExpression as IPredefinedTypeExpression;
        if (predefinedTypeExpr != null)
        {
          return this.ResolveTypeFromReference(predefinedTypeExpr.PredefinedTypeName.Reference);
        }
      }

      //Don't support keywords for now. There are 2 bugs with them.
      //return this.ResolveTypeForPredefinedType(context);
      return null;
    }

    [CanBeNull]
    private IDeclaredType ResolveTypeForPredefinedType([NotNull] CSharpCodeCompletionContext context)
    {
      var completionContext = context.UnterminatedContext ?? context.TerminatedContext;
      var previousToken = completionContext?.TreeNode?.GetPreviousMeaningfulToken();

      if (previousToken?.NodeType == CSharpTokenType.DOT) previousToken = previousToken?.GetPreviousMeaningfulToken();

      if (previousToken == null) return null;

      var predefinedTypeRef = previousToken.Parent as IPredefinedTypeReference;
      if (predefinedTypeRef != null)
      {
        return this.ResolveTypeFromReference(predefinedTypeRef.Reference);
      }


      return null;
    }


    [NotNull]
    private IEnumerable<IType> CreateCompletionTypes([NotNull] IDeclaredType currentType, [NotNull] CSharpCodeCompletionContext context)
    {
      var predefinedType = context.PsiModule.GetPredefinedType();

      var iEnumerableElement = predefinedType.GenericIEnumerable.GetTypeElement().NotNull();
      yield return TypeFactory.CreateType(iEnumerableElement, currentType);

      var iListElement = predefinedType.GenericIList.GetTypeElement().NotNull();
      yield return TypeFactory.CreateType(iListElement, currentType);

      var listElement = predefinedType.GenericList.GetTypeElement().NotNull();
      yield return TypeFactory.CreateType(listElement, currentType);
    }

    private class ContainerPostfixPresentation : TypePresentation
    {
      public ContainerPostfixPresentation([NotNull] TypeInfo info) : base(info)
      {
      }

      public override IconId Image => ServicesThemedIcons.LiveTemplate.Id;
    }
  }
}