import { Component, inject } from '@angular/core';
import { Router } from '@angular/router';
import { Auth } from '../core/auth';

/** "/" sends each person to the first screen their permissions open. */
@Component({ selector: 'app-landing', template: '' })
export class Landing {
  constructor() {
    void inject(Router).navigateByUrl(inject(Auth).home(), { replaceUrl: true });
  }
}
