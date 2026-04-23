---
status: draft
---

# Design Specification: Repositories VNext Feature (Domain)

> This design document outlines the architecture and behavior of a new Repository feature within the Domain layer. It defines the core concepts, goals, non-goals, high-level architecture, core design principles, public API and configuration, implementation details, testing strategies, and typical use cases for the Repositories VNext feature.

[TOC]

## 1. Introduction

current issues:

- too many extension methods (paging, results, filtering, etc), issue for mocking
- no native Result type support on repositories
- no native transaction support on repositories

improvements:

- easier to inherit/extend with new query methods. including that behaviors still work.
- better support for transactions and result types.
- native filtering support on repositories, no queryoptions needed then.
- ef only repositories, no need to support in-memory or other providers, so we can use ef features like tracking, etc. and also easier to maintain.
- better support for testing, no need to mock extension methods, etc.