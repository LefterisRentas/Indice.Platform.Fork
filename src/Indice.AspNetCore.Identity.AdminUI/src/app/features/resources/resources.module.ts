import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

import { SweetAlert2Module } from '@sweetalert2/ngx-sweetalert2';
import { SharedModule } from 'src/app/shared/shared.module';
import { ResourcesRoutingModule } from './resources-routing.module';
import { IdentityResourcesComponent } from './identity/identity-resources.component';
import { IdentityResourceEditComponent } from './identity/edit/identity-resource-edit.component';
import { IdentityResourceDetailsComponent } from './identity/edit/details/identity-resource-details.component';
import { IdentityResourceClaimsComponent } from './identity/edit/claims/identity-resource-claims.component';
import { IdentityResourceAddComponent } from './identity/add/identity-resource-add.component';

@NgModule({
    declarations: [
        IdentityResourcesComponent,
        IdentityResourceEditComponent,
        IdentityResourceDetailsComponent,
        IdentityResourceClaimsComponent,
        IdentityResourceAddComponent
    ],
    imports: [
        CommonModule,
        FormsModule,
        ResourcesRoutingModule,
        SharedModule,
        SweetAlert2Module
    ]
})
export class ResourcesModule { }