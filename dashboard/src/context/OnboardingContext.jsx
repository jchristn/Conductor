import React, { createContext, useContext, useState, useCallback, useEffect, useRef } from 'react';
import { useApp } from './AppContext';

const OnboardingContext = createContext(null);

export function OnboardingProvider({ children }) {
  const { isConnected } = useApp();

  // Tour state
  const [tourActive, setTourActive] = useState(false);
  const [tourStep, setTourStep] = useState(0);
  const [tourCompleted, setTourCompleted] = useState(() =>
    localStorage.getItem('conductor_tour_completed') === 'true'
  );

  // Wizard state
  const [wizardActive, setWizardActive] = useState(false);
  const [wizardStep, setWizardStep] = useState(0);
  const [wizardCompleted, setWizardCompleted] = useState(() =>
    localStorage.getItem('conductor_wizard_completed') === 'true'
  );

  // Pending create tracks which entity the wizard wants views to auto-open
  const [pendingCreate, setPendingCreate] = useState(null);

  // Track entity creation for wizard auto-advance
  const [lastCreatedEntity, setLastCreatedEntity] = useState(null);

  // Auto-start tour on first visit
  const autoStarted = useRef(false);
  useEffect(() => {
    if (isConnected && !tourCompleted && !autoStarted.current) {
      autoStarted.current = true;
      const timer = setTimeout(() => {
        setTourActive(true);
        setTourStep(0);
      }, 500);
      return () => clearTimeout(timer);
    }
  }, [isConnected, tourCompleted]);

  // Tour methods
  const startTour = useCallback(() => {
    setWizardActive(false);
    setTourStep(0);
    setTourActive(true);
  }, []);

  const nextTourStep = useCallback(() => {
    setTourStep(prev => prev + 1);
  }, []);

  const prevTourStep = useCallback(() => {
    setTourStep(prev => Math.max(0, prev - 1));
  }, []);

  const endTour = useCallback(() => {
    setTourActive(false);
    setTourStep(0);
    setTourCompleted(true);
    localStorage.setItem('conductor_tour_completed', 'true');
  }, []);

  // Wizard methods
  const startWizard = useCallback(() => {
    setTourActive(false);
    setWizardStep(0);
    setWizardActive(true);
  }, []);

  const nextWizardStep = useCallback(() => {
    setWizardStep(prev => prev + 1);
  }, []);

  const prevWizardStep = useCallback(() => {
    setWizardStep(prev => Math.max(0, prev - 1));
  }, []);

  const skipWizardStep = useCallback(() => {
    setWizardStep(prev => prev + 1);
  }, []);

  const endWizard = useCallback(() => {
    setWizardActive(false);
    setWizardStep(0);
    setPendingCreate(null);
    setWizardCompleted(true);
    localStorage.setItem('conductor_wizard_completed', 'true');
  }, []);

  const clearPendingCreate = useCallback(() => {
    setPendingCreate(null);
  }, []);

  const onEntityCreated = useCallback((type) => {
    setLastCreatedEntity(type);
  }, []);

  const value = {
    tourActive,
    tourStep,
    tourCompleted,
    wizardActive,
    wizardStep,
    wizardCompleted,
    pendingCreate,
    lastCreatedEntity,
    startTour,
    nextTourStep,
    prevTourStep,
    endTour,
    startWizard,
    nextWizardStep,
    prevWizardStep,
    skipWizardStep,
    endWizard,
    setPendingCreate,
    clearPendingCreate,
    onEntityCreated,
    setLastCreatedEntity
  };

  return <OnboardingContext.Provider value={value}>{children}</OnboardingContext.Provider>;
}

export function useOnboarding() {
  const context = useContext(OnboardingContext);
  if (!context) {
    throw new Error('useOnboarding must be used within OnboardingProvider');
  }
  return context;
}

export default OnboardingContext;
