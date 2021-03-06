﻿using System;
using System.Collections.Generic;
using System.Linq;
using com.tinylabproductions.TLPLib.Concurrent;
using com.tinylabproductions.TLPLib.Extensions;
using com.tinylabproductions.TLPLib.Reactive;
using JetBrains.Annotations;

namespace com.tinylabproductions.TLPLib.Functional {
  /// <summary>
  /// C# does not have higher kinded types, so we need to specify every combination of monads here
  /// as an extension method. However this reduces visual noise in the usage sites.
  /// </summary>
  public static class MonadTransformers {
    #region Option

    #region of Either

    [PublicAPI] public static Option<Either<A, BB>> mapT<A, B, BB>(
      this Option<Either<A, B>> m, Fn<B, BB> mapper
    ) => m.map(_ => _.mapRight(mapper));

    [PublicAPI] public static Option<Either<A, BB>> flatMapT<A, B, BB>(
      this Option<Either<A, B>> m, Fn<B, Either<A, BB>> mapper
    ) => m.map(_ => _.flatMapRight(mapper));

    [PublicAPI] public static Either<A, Option<B>> extract<A, B>(this Option<Either<A, B>> o) {
      if (o.isSome) {
        var e = o.__unsafeGetValue;
        return
          e.isLeft
            ? Either<A, Option<B>>.Left(e.__unsafeGetLeft)
            : Either<A, Option<B>>.Right(e.__unsafeGetRight.some());
      }
      return F.none<B>();
    }

    #endregion

    #region of IEnumerable

    [PublicAPI] public static IEnumerable<Option<A>> extract<A>(
      this Option<IEnumerable<A>> opt
    ) =>
      opt.isNone
        ? Enumerable.Empty<Option<A>>()
        : opt.__unsafeGetValue.Select(a => a.some());

    #endregion

    #endregion

    #region Either

    #region ofOption

    [PublicAPI] public static Either<A, Option<BB>> mapT<A, B, BB>(
      this Either<A, Option<B>> m, Fn<B, BB> mapper
    ) => m.mapRight(_ => _.map(mapper));

    [PublicAPI] public static Either<A, Option<BB>> flatMapT<A, B, BB>(
      this Either<A, Option<B>> m, Fn<B, Option<BB>> mapper
    ) => m.mapRight(_ => _.flatMap(mapper));

    #endregion

    #endregion

    #region Future

    #region of Option

    [PublicAPI] public static Future<Option<B>> mapT<A, B>(
      this Future<Option<A>> m, Fn<A, B> mapper
    ) => m.map(_ => _.map(mapper));

    [PublicAPI] public static Future<Option<B>> flatMapT<A, B>(
      this Future<Option<A>> m, Fn<A, Option<B>> mapper
    ) => m.map(_ => _.flatMap(mapper));

    [PublicAPI] public static Future<Option<B>> flatMapT<A, B>(
      this Future<Option<A>> m, Fn<A, Future<Option<B>>> mapper
    ) => m.flatMap(_ => _.fold(
      () => Future.successful(F.none<B>()),
      mapper
    ));

    #endregion

    #region of Either

    [PublicAPI] public static Future<Either<A, BB>> mapT<A, B, BB>(
      this Future<Either<A, B>> m, Fn<B, BB> mapper
    ) => m.map(_ => _.mapRight(mapper));

    [PublicAPI] public static Future<Either<A, BB>> flatMapT<A, B, BB>(
      this Future<Either<A, B>> m, Fn<B, Either<A, BB>> mapper
    ) => m.map(_ => _.flatMapRight(mapper));

    [PublicAPI] public static Future<Either<A, BB>> flatMapT<A, B, BB>(
      this Future<Either<A, B>> m, Fn<B, Future<Either<A, BB>>> mapper
    ) => m.flatMap(_ => _.fold(
      a => Future.successful(Either<A, BB>.Left(a)),
      mapper
    ));

    [PublicAPI] public static Future<Either<A, BB>> flatMapT<B, BB, A>(
      this Future<Either<A, B>> m, Fn<B, Future<BB>> mapper
    ) => m.flatMap(_ => _.fold(
      err => Future.successful(Either<A, BB>.Left(err)),
      from => mapper(from).map(Either<A, BB>.Right)
    ));


    #endregion

    #region of Try

    [PublicAPI] public static Future<Try<To>> flatMapT<From, To>(
      this Future<Try<From>> m, Fn<From, Future<To>> mapper
    ) => m.flatMap(_ => _.fold(
      from => mapper(from).map(F.scs),
      err => Future.successful(F.err<To>(err))
    ));

    #endregion

    #endregion

    #region LazyVal

    #region of Option

    [PublicAPI]
    public static LazyVal<Option<B>> lazyMapT<A, B>(
      this LazyVal<Option<A>> m, Fn<A, B> mapper
    ) => m.lazyMap(_ => _.map(mapper));

    [PublicAPI]
    public static LazyVal<Option<B>> lazyFlatMapT<A, B>(
      this LazyVal<Option<A>> m, Fn<A, Option<B>> mapper
    ) => m.lazyMap(_ => _.flatMap(mapper));

    #endregion

    #region of Try

    [PublicAPI] public static LazyVal<Try<B>> lazyMapT<A, B>(
      this LazyVal<Try<A>> m, Fn<A, B> mapper
    ) => m.lazyMap(_ => _.map(mapper));

    #endregion

    #region of Future

    [PublicAPI] public static LazyVal<Future<B>> lazyMapT<A, B>(
      this LazyVal<Future<A>> m, Fn<A, B> mapper
    ) => m.lazyMap(_ => _.map(mapper));

    #endregion

    #endregion

    #region IRxVal

    #region of Option

    [PublicAPI] public static IRxVal<Option<B>> mapT<A, B>(
      this IRxVal<Option<A>> rxMaybeA, Fn<A, B> f
    ) => rxMaybeA.map(maybeA => maybeA.map(f));

    #endregion

    #endregion
  }
}