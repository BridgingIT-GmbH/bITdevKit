---
status: draft
---

# Design Document: Repositories VNext Feature (Domain)

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
 -