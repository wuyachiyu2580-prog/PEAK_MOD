using System;
using Zorro.UI;

namespace StateKeeper
{
    internal sealed class StateKeeperPage : UIPage, IHaveParentPage
    {
        internal UIPage ParentPage { get; set; }
        internal Action OnOpened { get; set; }

        public override void OnPageEnter()
        {
            base.OnPageEnter();
            if (OnOpened != null) OnOpened();
        }

        public (UIPage, PageTransistion) GetParentPage()
        {
            return (ParentPage, new SetActivePageTransistion());
        }

        public bool OnAttemptGoToParent() { return !StateKeeperUi.DismissRename(); }

        public override void OnPageExit()
        {
            StateKeeperUi.DismissRename();
            base.OnPageExit();
        }
    }

    internal sealed class StateKeeperDetailsPage : UIPage, IHaveParentPage
    {
        internal UIPage ParentPage { get; set; }
        internal Action OnOpened { get; set; }
        internal Action OnClosed { get; set; }
        internal Action OnTick { get; set; }

        public override void OnPageEnter()
        {
            base.OnPageEnter();
            if (OnOpened != null) OnOpened();
        }

        public (UIPage, PageTransistion) GetParentPage()
        {
            return (ParentPage, new SetActivePageTransistion());
        }

        public bool OnAttemptGoToParent() { return true; }

        public override void OnPageExit()
        {
            if (OnClosed != null) OnClosed();
            base.OnPageExit();
        }

        private void Update()
        {
            if (OnTick != null) OnTick();
        }
    }
}
