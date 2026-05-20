```mermaid
stateDiagram-v2
    [*] --> init
    init --> researching : start
    researching --> implementing : research_done
    implementing --> validating : impl_done
    validating --> fixing : build_failed
    validating --> fixing : tests_failed
    validating --> reviewing : clean
    reviewing --> done : approved
    reviewing --> fixing-pr : changes_requested
    fixing-pr --> validating-pr : fix_done
    validating-pr --> fixing-pr : build_failed
    validating-pr --> fixing-pr : tests_failed
    validating-pr --> reviewing-signoff : clean
    reviewing-signoff --> done : approved
    reviewing-signoff --> fixing-pr : changes_requested
    fixing --> validating : fix_done
    fixing --> failed : max_retries
    fixing-pr --> failed : max_retries
    done --> [*]
    failed --> [*]
```
